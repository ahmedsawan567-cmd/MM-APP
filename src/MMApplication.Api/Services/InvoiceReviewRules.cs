using Microsoft.EntityFrameworkCore;
using MMApplication.Application.Common;
using MMApplication.Application.DTOs;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Services;

/// <summary>قواعد القبول التلقائي للفواتير (مشتركة بين الرفع وإعادة القراءة التلقائية).</summary>
public static class InvoiceReviewRules
{
    /// <summary>
    /// قواعد التحويل لمراجعة الموظفين بدل القبول التلقائي. القائمة الفارغة = قبول تلقائي.
    /// الحدود قابلة للتعديل من appsettings تحت القسم "InvoiceReview".
    /// </summary>
    public static async Task<List<string>> CollectReasonsAsync(AppDbContext _context, IConfiguration? _configuration, IHostEnvironment? _environment,
        ExtractedReceiptDto extracted, string? tenantId, DateTime invoiceDate, decimal totalAmount, int fraudRiskScore)
    {
        var reasons = new List<string>();
        var settings = await ReviewSettings.LoadAsync(_context, _configuration);
        var minConfidence = settings.MinConfidence;
        var amountThreshold = settings.ReviewAmountAbove;

        if (extracted.RequiresManualReview)
        {
            reasons.Add("تعذّرت القراءة الآلية للفاتورة");
            reasons.AddRange(extracted.AuthenticityConcerns);
            return reasons;
        }

        if (fraudRiskScore >= 35) reasons.Add("الجهاز مستخدم من حسابات عملاء أخرى");
        if (extracted.Confidence < minConfidence) reasons.Add($"ثقة القراءة منخفضة ({extracted.Confidence:P0})");
        reasons.AddRange(extracted.AuthenticityConcerns.Select(c => $"ملاحظة الذكاء الاصطناعي: {c}"));
        var requireKnownStore = settings.RequireKnownStore;
        if (requireKnownStore && string.IsNullOrEmpty(tenantId)) reasons.Add("المتجر غير معروف ضمن متاجر المول");

        // مطابقة بصمة فاتورة المتجر (إن تعلّمها النظام)
        if (!string.IsNullOrEmpty(tenantId))
        {
            var profile = await _context.StoreInvoiceProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsActive);
            if (profile != null)
            {
                if (InvoiceNumbers.MatchesPattern(extracted.InvoiceNumber, profile.InvoiceNumberPattern) == false)
                    reasons.Add($"رقم الفاتورة ({extracted.InvoiceNumber}) لا يطابق نمط فواتير المتجر (مثال: {profile.InvoiceNumberExample})");
                if (!string.IsNullOrWhiteSpace(profile.TaxNumber) && !string.IsNullOrWhiteSpace(extracted.TaxNumber) &&
                    !InvoiceNumbers.SameIdentifier(profile.TaxNumber, extracted.TaxNumber))
                    reasons.Add($"الرقم الضريبي على الفاتورة ({extracted.TaxNumber}) يختلف عن رقم المتجر المسجل");
            }
        }
        if (totalAmount > amountThreshold) reasons.Add($"مبلغ مرتفع يتجاوز {amountThreshold:N0} د.ل");
        if (!string.IsNullOrEmpty(extracted.Currency) &&
            !extracted.Currency.Equals("LYD", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"عملة غير الدينار الليبي ({extracted.Currency})");
        }

        if (extracted.InvoiceDate.HasValue)
        {
            var maxAgeDays = 7;
            if (!string.IsNullOrEmpty(tenantId))
            {
                var rule = await _context.TenantLoyaltyRules.AsNoTracking().FirstOrDefaultAsync(r => r.TenantId == tenantId);
                if (rule != null && rule.MaxInvoiceAgeDays > 0) maxAgeDays = rule.MaxInvoiceAgeDays;
            }
            if (invoiceDate.Date < DateTime.UtcNow.Date.AddDays(-maxAgeDays)) reasons.Add($"الفاتورة أقدم من {maxAgeDays} أيام");
            if (invoiceDate.Date > DateTime.UtcNow.Date.AddDays(1)) reasons.Add("تاريخ الفاتورة في المستقبل");
        }

        // المحرك الوهمي مسموح بالقبول التلقائي في بيئة التطوير والاختبار فقط
        var isDevOrTest = _environment == null || _environment.IsDevelopment() || _environment.IsEnvironment("Testing");
        if (extracted.Provider == "Mock" && !isDevOrTest) reasons.Add("القراءة الآلية بالذكاء الاصطناعي غير مفعّلة");

        return reasons;
    }
}

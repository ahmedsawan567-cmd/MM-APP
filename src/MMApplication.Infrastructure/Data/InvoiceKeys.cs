using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMApplication.Application.Common;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data;

/// <summary>كشف تكرار أرقام الفواتير (مكافحة الاحتيال).</summary>
public static class InvoiceKeys
{
    /// <summary>
    /// يبحث عن فاتورة سابقة بنفس الرقم (بصيغته الموحّدة) لنفس المتجر — بكل الحالات بما فيها المرفوضة والملغاة،
    /// لأن رقم الفاتورة يُستخدم مرة واحدة فقط. للفواتير مجهولة المتجر: نفس الرقم + نفس المبلغ + نفس التاريخ.
    /// </summary>
    public static async Task<Invoice?> FindDuplicateAsync(
        AppDbContext db, string? tenantId, string? numberKey, decimal? total, DateTime? date,
        Guid? excludeInvoiceId = null, string? merchantName = null)
    {
        if (string.IsNullOrEmpty(numberKey)) return null;
        var query = db.Invoices.AsNoTracking().Where(i => i.InvoiceNumberKey == numberKey);
        if (excludeInvoiceId is { } ex) query = query.Where(i => i.InvoiceId != ex);

        // الفواتير بنفس الرقم قليلة جداً، فنقارن بقية الشروط في الذاكرة
        var candidates = await query.OrderBy(i => i.CreatedAt).ToListAsync();
        if (candidates.Count == 0) return null;

        // 1) نفس المتجر
        if (!string.IsNullOrEmpty(tenantId) && candidates.FirstOrDefault(i => i.TenantId == tenantId) is { } sameStore)
            return sameStore;

        // 2) متجر غير معروف: نفس اسم المتجر كما قُرئ
        var name = merchantName?.Trim();
        if (string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(name) &&
            candidates.FirstOrDefault(i => string.Equals(i.ExtractedMerchant?.Trim(), name, StringComparison.OrdinalIgnoreCase)) is { } sameName)
            return sameName;

        // 3) نفس الرقم والمبلغ والتاريخ (حتى لو قُرئ اسم المتجر بشكل مختلف)
        if (total is > 0 && date.HasValue)
            return candidates.FirstOrDefault(i => i.TotalAmount == total && i.InvoiceDate?.Date == date.Value.Date);

        return null;
    }

    /// <summary>الفواتير القديمة: حساب الصيغة الموحّدة. عند تعارض قديم يبقى الأقدم ويُترك الأحدث بدون مفتاح.</summary>
    public static async Task<int> BackfillAsync(AppDbContext db, ILogger logger)
    {
        var missing = await db.Invoices
            .Where(i => i.InvoiceNumberKey == null && i.InvoiceNumber != null && i.InvoiceNumber != "")
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();
        if (missing.Count == 0) return 0;

        var taken = (await db.Invoices.AsNoTracking()
                .Where(i => i.InvoiceNumberKey != null)
                .Select(i => new { i.TenantId, i.InvoiceNumberKey })
                .ToListAsync())
            .Select(x => (x.TenantId, x.InvoiceNumberKey))
            .ToHashSet();

        var count = 0;
        foreach (var invoice in missing)
        {
            var key = InvoiceNumbers.Key(invoice.InvoiceNumber);
            if (key == null) continue;
            if (!taken.Add((invoice.TenantId, key)))
            {
                logger.LogWarning("فاتورة مكررة قديمة: {InvoiceId} ({Number}) تطابق فاتورة سابقة لنفس المتجر.", invoice.InvoiceId, invoice.InvoiceNumber);
                continue;
            }
            invoice.InvoiceNumberKey = key;
            count++;
        }
        await db.SaveChangesAsync();
        return count;
    }

}

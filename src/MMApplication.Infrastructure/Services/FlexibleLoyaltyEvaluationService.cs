using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Infrastructure.Services;

public class FlexibleLoyaltyEvaluationService : IFlexibleLoyaltyEvaluationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FlexibleLoyaltyEvaluationService>? _logger;

    public FlexibleLoyaltyEvaluationService(
        AppDbContext context,
        ILogger<FlexibleLoyaltyEvaluationService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LoyaltyEvaluationResult> EvaluateInvoicePointsAsync(LoyaltyEvaluationRequest request)
    {
        var result = new LoyaltyEvaluationResult();
        var totalAmount = request.TotalAmount;
        var invoiceDate = request.InvoiceDate;

        // 1. استرجاع قاعدة الاكتساب المحددة للمتجر، أو القاعدة العامة للمول
        LoyaltyEarningRule? activeRule = null;

        if (!string.IsNullOrEmpty(request.TenantId))
        {
            activeRule = await _context.LoyaltyEarningRules
                .Where(r => r.IsActive && r.StoreTenantId == request.TenantId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (activeRule == null)
        {
            activeRule = await _context.LoyaltyEarningRules
                .Where(r => r.IsActive && r.StoreTenantId == null)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
        }

        // إذا لم توجد قاعدة مسجلة في قاعدة البيانات، نستخدم القاعدة الافتراضية القياسية (Spend-based 1 pt per 10 LYD)
        var ruleName = activeRule?.RuleName ?? "قاعدة الولاء العامة الافتراضية";
        var calcType = activeRule?.CalculationType?.ToUpperInvariant() ?? "SPEND_RATIO";
        var fixedVisitPoints = activeRule?.FixedPointsPerInvoice ?? 0;
        var spendRatio = activeRule?.SpendRatioRate ?? 0.1000m;
        var minSpend = activeRule?.MinInvoiceAmount ?? 0.00m;
        var maxCap = activeRule?.MaxPointsCap;

        result.AppliedRuleName = ruleName;
        result.CalculationType = calcType;

        // فحص الحد الأدنى للإنفاق
        if (totalAmount < minSpend)
        {
            result.Remarks.Add($"لم تحقق الفاتورة الحد الأدنى لكسب النقاط ({minSpend:N0} د.ل).");
            result.PointsBreakdownJson = JsonSerializer.Serialize(new
            {
                ruleName,
                calcType,
                minSpendRequired = minSpend,
                totalAmount,
                reason = "BELOW_MINIMUM_SPEND",
                totalPointsAwarded = 0
            });
            return result;
        }

        // 2. احتساب النقاط الأساسية حسب النمط (Mode Calculation)
        var baseVisitPoints = 0;
        var baseSpendPoints = 0;

        switch (calcType)
        {
            case "FIXED_PER_INVOICE":
                // النمط 1: نقطة ثابتة لكل فاتورة/زيارة
                baseVisitPoints = fixedVisitPoints > 0 ? fixedVisitPoints : 5;
                result.Remarks.Add($"مكافأة زيارة ثابتة (+{baseVisitPoints} نقطة)");
                break;

            case "SPEND_RATIO":
                // النمط 2: النقاط حسب إجمالي المبلغ
                baseSpendPoints = (int)Math.Floor(totalAmount * spendRatio);
                result.Remarks.Add($"نقاط الإنفاق: {totalAmount:N2} د.ل بمعدل ({spendRatio}) = +{baseSpendPoints} نقطة");
                break;

            case "ITEM_BOOSTER":
                // النمط 3: بونص الأصناف وسلة المشتريات فقط
                baseSpendPoints = 0;
                baseVisitPoints = 0;
                break;

            case "HYBRID":
            default:
                // النمط 4: هجين (نقاط الزيارة + نقاط الإنفاق)
                baseVisitPoints = fixedVisitPoints;
                baseSpendPoints = (int)Math.Floor(totalAmount * spendRatio);
                if (baseVisitPoints > 0) result.Remarks.Add($"مكافأة زيارة (+{baseVisitPoints} نقطة)");
                if (baseSpendPoints > 0) result.Remarks.Add($"نقاط الإنفاق (+{baseSpendPoints} نقطة)");
                break;
        }

        var unmultipliedBasePoints = baseVisitPoints + baseSpendPoints;

        // 3. فحص ومطابقة محفزات الأصناف والماركات وسلة المشتريات (Item / SKU Boosters)
        var boosterBonusPoints = 0;
        var activeBoosters = await _context.ProductBoosterRules
            .Where(b => b.IsActive && b.StartDate <= invoiceDate && b.EndDate >= invoiceDate && (b.TenantId == null || b.TenantId == request.TenantId))
            .ToListAsync();

        if (activeBoosters.Any())
        {
            var itemDescriptions = request.Items?.Select(li => li.Description).Where(d => !string.IsNullOrWhiteSpace(d)).ToList() ?? new List<string>();
            var merchantName = request.MerchantName ?? string.Empty;

            foreach (var booster in activeBoosters)
            {
                var kw = booster.BrandOrItemKeyword?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(kw)) continue;

                var isMatched = itemDescriptions.Any(txt => txt.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    || merchantName.Contains(kw, StringComparison.OrdinalIgnoreCase);

                if (isMatched)
                {
                    boosterBonusPoints += booster.BonusPoints;
                    result.MatchedBoosters.Add(new MatchedBoosterDetail
                    {
                        BoosterId = booster.Id,
                        Title = booster.Title,
                        Keyword = booster.BrandOrItemKeyword,
                        BonusPoints = booster.BonusPoints,
                        PointsMultiplier = booster.PointsMultiplier
                    });
                    result.Remarks.Add($"بونص صنف: {booster.Title} (+{booster.BonusPoints} نقطة)");
                }
            }
        }

        // 4. تطبيق مضاعف فئة العضوية (Tier Multiplier) والحملات الترويجية
        // مضاعف العضوية من إعدادات العضويات في المنظومة
        var tierMultiplier = await MembershipTiers.MultiplierAsync(_context, request.CustomerTier);

        // فحص وجود حملة مضاعفة نشطة في المول
        var activeCampaign = await _context.LoyaltyCampaigns
            .Where(c => c.IsActive && c.StartDate <= invoiceDate && c.EndDate >= invoiceDate && totalAmount >= c.MinSpendAmount)
            .OrderByDescending(c => c.PointsMultiplier)
            .FirstOrDefaultAsync();

        var campaignMultiplier = activeCampaign?.PointsMultiplier ?? 1.00m;
        var effectiveMultiplier = Math.Max(tierMultiplier, campaignMultiplier);

        if (effectiveMultiplier > 1.0m && unmultipliedBasePoints > 0)
        {
            var multiplied = (int)Math.Round(unmultipliedBasePoints * effectiveMultiplier);
            result.Remarks.Add($"مضاعف العضوية/الحملة ({effectiveMultiplier}x): {unmultipliedBasePoints} -> {multiplied} نقطة");
            result.BasePointsEarned = multiplied;
        }
        else
        {
            result.BasePointsEarned = unmultipliedBasePoints;
        }

        result.ItemBonusPointsEarned = boosterBonusPoints;
        var calculatedTotal = result.BasePointsEarned + result.ItemBonusPointsEarned;

        // 5. تطبيق السقف الأقصى للنقاط إن وجد (Max Points Cap)
        if (maxCap.HasValue && maxCap.Value > 0 && calculatedTotal > maxCap.Value)
        {
            result.Remarks.Add($"تم تطبيق سقف النقاط الأقصى للفاتورة: {maxCap.Value} نقطة (بدلاً من {calculatedTotal})");
            result.TotalPointsAwarded = maxCap.Value;
        }
        else
        {
            result.TotalPointsAwarded = calculatedTotal;
        }

        // كاش باك تقديري 5%
        result.CashbackAwarded = totalAmount * 0.05m;

        // 6. تشكيل تفاصيل الـ Breakdown JSON للحفظ الدائم
        var breakdownObj = new
        {
            ruleName,
            calculationType = calcType,
            invoiceSpendAmount = totalAmount,
            fixedVisitPoints = baseVisitPoints,
            spendBasePoints = baseSpendPoints,
            unmultipliedBase = unmultipliedBasePoints,
            customerTier = request.CustomerTier,
            tierMultiplier,
            campaignName = activeCampaign?.Name,
            campaignMultiplier = activeCampaign?.PointsMultiplier,
            effectiveMultiplier,
            basePointsFinal = result.BasePointsEarned,
            itemBonusPointsTotal = result.ItemBonusPointsEarned,
            matchedBoosters = result.MatchedBoosters,
            maxPointsCap = maxCap,
            totalPointsAwarded = result.TotalPointsAwarded,
            calculatedAt = DateTime.UtcNow
        };

        result.PointsBreakdownJson = JsonSerializer.Serialize(breakdownObj, new JsonSerializerOptions { WriteIndented = false });

        return result;
    }
}

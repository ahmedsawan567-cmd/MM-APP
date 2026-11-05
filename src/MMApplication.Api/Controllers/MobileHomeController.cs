using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Application.DTOs;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/mobile")]
[Produces("application/json")]
public class MobileHomeController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<MobileHomeController> _logger;

    public MobileHomeController(AppDbContext context, ILogger<MobileHomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// واجهة الصفحة الرئيسية الموجهة من السيرفر لتطبيق الجوال (Server-Driven Mobile Home)
    /// تقدم البنرات المخصصة حسب شريحة المتسوق، التصنيفات السريعة، محفزات النقاط الساخنة، والمتاجر المميزة.
    /// </summary>
    [HttpGet("home")]
    [ProducesResponseType(typeof(MobileHomeResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHomeLayout()
    {
        var loggedInUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        UserProfileHomeDto? userProfile = null;
        var userTier = "BRONZE";

        if (!string.IsNullOrWhiteSpace(loggedInUserId))
        {
            var customer = await _context.Customers
                .Include(c => c.Wallets)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == loggedInUserId);

            if (customer != null)
            {
                userTier = customer.LoyaltyTier?.ToUpperInvariant() ?? "BRONZE";
                var pointsWallet = customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS);
                var balance = (int)(pointsWallet?.CurrentBalance ?? 0);

                var tierMultiplier = userTier switch
                {
                    "PLATINUM" => 2.0m,
                    "GOLD" or "VIP" => 1.5m,
                    "SILVER" => 1.25m,
                    _ => 1.0m
                };

                var (nextTier, needed, progress) = CalculateTierProgress(userTier, balance);

                userProfile = new UserProfileHomeDto
                {
                    CustomerId = customer.CustomerId,
                    FullName = customer.FullName,
                    LoyaltyTier = userTier,
                    PointsBalance = balance,
                    TierMultiplier = tierMultiplier,
                    NextTierName = nextTier,
                    PointsToNextTier = needed,
                    TierProgressPercentage = progress
                };
            }
        }

        // 1. استرجاع البنرات المفلترة حسب شريحة المتسوق
        var now = DateTime.UtcNow;
        var bannersQuery = _context.PromotionBanners
            .Where(b => b.IsActive && (b.StartDate == null || b.StartDate <= now) && (b.EndDate == null || b.EndDate >= now))
            .AsNoTracking();

        var allBanners = await bannersQuery.OrderBy(b => b.DisplayOrder).ThenByDescending(b => b.CreatedAt).ToListAsync();

        var filteredBanners = allBanners.Where(b =>
        {
            var aud = b.TargetAudience?.ToUpperInvariant() ?? "ALL";
            if (aud == "ALL") return true;
            if (userProfile == null) return aud == "NEW_USERS";

            return aud switch
            {
                "PLATINUM_ONLY" or "PLATINUM" => userTier == "PLATINUM",
                "GOLD_ONLY" or "GOLD" => userTier is "GOLD" or "PLATINUM",
                "VIP_GOLD" or "VIP" => userTier is "GOLD" or "PLATINUM" or "VIP",
                "SILVER" => userTier is "SILVER" or "GOLD" or "PLATINUM",
                "BRONZE" => true,
                "NEW_USERS" => false,
                _ => true
            };
        })
        .Select(b => new MobileBannerDto
        {
            Id = b.Id,
            Title = b.Title,
            Subtitle = b.Subtitle,
            ImageUrl = b.ImageUrl,
            ActionType = b.ActionType ?? "EXTERNAL_URL",
            ActionPayloadJson = b.ActionPayloadJson,
            TargetUrl = b.TargetUrl,
            DisplayOrder = b.DisplayOrder
        })
        .ToList();

        // 2. التصنيفات السريعة مع أيقوناتها وعدد المتاجر
        var storesByCategory = await _context.Tenants
            .Where(t => t.IsActive)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);

        var quickCategories = new List<MobileCategoryDto>
        {
            new() { Key = "FASHION", NameAr = "أزياء وملابس", NameEn = "Fashion", IconName = "shirt", ColorHex = "#f59e0b", StoresCount = storesByCategory.GetValueOrDefault("أزياء وموضة", 12) },
            new() { Key = "SUPERMARKET", NameAr = "سوبرماركت", NameEn = "Supermarket", IconName = "shopping-cart", ColorHex = "#10b981", StoresCount = storesByCategory.GetValueOrDefault("سوبرماركت", 3) },
            new() { Key = "CAFE", NameAr = "مطاعم ومقاهي", NameEn = "Cafes & Dining", IconName = "coffee", ColorHex = "#f97316", StoresCount = storesByCategory.GetValueOrDefault("مطاعم وكافيهات", 18) },
            new() { Key = "ELECTRONICS", NameAr = "إلكترونيات", NameEn = "Electronics", IconName = "smartphone", ColorHex = "#3b82f6", StoresCount = storesByCategory.GetValueOrDefault("إلكترونيات وتكنولوجيا", 8) },
            new() { Key = "BEAUTY", NameAr = "عطور وتجميل", NameEn = "Perfumes & Beauty", IconName = "sparkles", ColorHex = "#ec4899", StoresCount = storesByCategory.GetValueOrDefault("عطور ومستحضرات تجميل", 9) },
            new() { Key = "ENTERTAINMENT", NameAr = "ترفيه وسينما", NameEn = "Entertainment", IconName = "film", ColorHex = "#8b5cf6", StoresCount = storesByCategory.GetValueOrDefault("ترفيه وسينما", 4) }
        };

        // 3. محفزات وعروض الأصناف الساخنة (Hot Booster Offers)
        var activeBoosters = await _context.ProductBoosterRules
            .Include(r => r.Tenant)
            .Where(r => r.IsActive && r.StartDate <= now && r.EndDate >= now)
            .OrderByDescending(r => r.BonusPoints)
            .Take(6)
            .Select(r => new MobileBoosterOfferDto
            {
                BoosterId = r.Id,
                Title = r.Title,
                Keyword = r.BrandOrItemKeyword,
                StoreName = r.Tenant != null ? r.Tenant.NameAr : "كافة المتاجر",
                BonusPoints = r.BonusPoints,
                PointsMultiplier = r.PointsMultiplier,
                EndDate = r.EndDate,
                DaysRemaining = Math.Max(0, (r.EndDate - now).Days)
            })
            .ToListAsync();

        // 4. المتاجر المميزة (Top Spotlight Stores)
        var topStores = await _context.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.NameAr)
            .Take(8)
            .Select(t => new MobileStoreSpotlightDto
            {
                TenantId = t.TenantId,
                NameAr = t.NameAr,
                UnitNumber = t.UnitNumber,
                Category = t.Category,
                LogoUrl = t.LogoUrl,
                FloorLabel = t.UnitNumber.StartsWith("G") ? "الطابق الأرضي" : "الطابق الأول"
            })
            .ToListAsync();

        var response = new MobileHomeResponseDto
        {
            User = userProfile,
            HeroBanners = filteredBanners,
            QuickCategories = quickCategories,
            HotBoosters = activeBoosters,
            TopStores = topStores,
            LayoutMeta = new ServerDrivenUiMetaDto
            {
                AppVersionRequired = "1.0.0",
                MaintenanceMode = false,
                ActiveTheme = userTier == "PLATINUM" ? "platinum-dark" : (userTier == "GOLD" ? "gold-dark" : "slate-dark")
            }
        };

        return Ok(response);
    }

    private static (string NextTier, int PointsNeeded, decimal Progress) CalculateTierProgress(string tier, int currentPoints)
    {
        return tier switch
        {
            "BRONZE" => ("SILVER", Math.Max(0, 500 - currentPoints), Math.Min(100m, (currentPoints / 500m) * 100m)),
            "SILVER" => ("GOLD", Math.Max(0, 1500 - currentPoints), Math.Min(100m, ((currentPoints - 500) / 1000m) * 100m)),
            "GOLD" or "VIP" => ("PLATINUM", Math.Max(0, 5000 - currentPoints), Math.Min(100m, ((currentPoints - 1500) / 3500m) * 100m)),
            _ => ("MAX_TIER", 0, 100m)
        };
    }

    /// <summary>
    /// دليل المتاجر النشطة لتطبيق الجوال (عام، بدون بيانات حساسة مثل المبيعات أو أرقام الموظفين).
    /// </summary>
    [HttpGet("stores")]
    [ProducesResponseType(typeof(IEnumerable<MobileStoreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStores([FromQuery] string? search, [FromQuery] string? category)
    {
        var query = _context.Tenants
            .Include(t => t.LoyaltyRule)
            .AsNoTracking()
            .Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t => t.NameAr.Contains(s) || t.NameEn.Contains(s) || t.Category.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
        {
            query = query.Where(t => t.Category == category);
        }

        var stores = await query
            .OrderBy(t => t.NameAr)
            .Select(t => new MobileStoreDto
            {
                TenantId = t.TenantId,
                NameAr = t.NameAr,
                NameEn = t.NameEn,
                UnitNumber = t.UnitNumber,
                Category = t.Category,
                LogoUrl = t.LogoUrl,
                FloorLabel = t.FloorNumber != "" ? t.FloorNumber : (t.UnitNumber.StartsWith("G") ? "الطابق الأرضي" : "الطابق الأول"),
                RewardType = t.LoyaltyRule != null ? t.LoyaltyRule.RewardType : "POINTS",
                ConversionRate = t.LoyaltyRule != null ? t.LoyaltyRule.ConversionRate : 0.1m
            })
            .ToListAsync();

        return Ok(stores);
    }

    /// <summary>
    /// صفحة المتجر في التطبيق: كيف أكسب النقاط هنا، رصيدي، وكم يتبقى لي لكل جائزة
    /// (مع تقدير المشتريات اللازمة للوصول إليها). بيانات العميل تظهر فقط إن كان مسجلاً دخوله.
    /// </summary>
    [HttpGet("stores/{id}")]
    public async Task<IActionResult> GetStoreDetails(string id)
    {
        var store = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == id && t.IsActive);
        if (store == null) return NotFound(new { success = false, message = "المتجر غير موجود." });

        // قاعدة الكسب الفعلية: الخاصة بالمتجر، ثم العامة للمول، ثم الافتراضية (نفس محرك احتساب النقاط)
        var rule = await _context.LoyaltyEarningRules.AsNoTracking()
                       .Where(r => r.IsActive && r.StoreTenantId == id).OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync()
                   ?? await _context.LoyaltyEarningRules.AsNoTracking()
                       .Where(r => r.IsActive && r.StoreTenantId == null).OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
        var calc = rule?.CalculationType?.ToUpperInvariant() ?? "SPEND_RATIO";
        var ratio = rule?.SpendRatioRate ?? 0.1m;
        var fixedPoints = rule?.FixedPointsPerInvoice ?? 0;
        var minSpend = rule?.MinInvoiceAmount ?? 0m;
        var tenantRule = await _context.TenantLoyaltyRules.AsNoTracking().FirstOrDefaultAsync(r => r.TenantId == id);
        var maxAge = tenantRule?.MaxInvoiceAgeDays is > 0 ? tenantRule.MaxInvoiceAgeDays : 7;

        string Lyd(decimal v) => v == Math.Round(v) ? v.ToString("N0") : v.ToString("N2");
        var earn = new List<string>();
        if (calc is "SPEND_RATIO" or "HYBRID" && ratio > 0) earn.Add($"نقطة واحدة لكل {Lyd(1 / ratio)} د.ل تشتريها");
        if (calc is "FIXED_PER_INVOICE" or "HYBRID" && fixedPoints > 0) earn.Add($"{fixedPoints} نقطة مع كل فاتورة");
        if (minSpend > 0) earn.Add($"أقل مبلغ للفاتورة {Lyd(minSpend)} د.ل");
        if (rule?.MaxPointsCap is > 0) earn.Add($"حتى {rule.MaxPointsCap} نقطة للفاتورة الواحدة");
        earn.Add($"صوّر الفاتورة خلال {maxAge} أيام من الشراء");

        var now = DateTime.UtcNow;
        var boosters = await _context.ProductBoosterRules.AsNoTracking()
            .Where(b => b.IsActive && b.StartDate <= now && b.EndDate >= now && (b.TenantId == id || b.TenantId == null))
            .OrderByDescending(b => b.BonusPoints)
            .Select(b => new { b.Title, b.BonusPoints, endsAt = b.EndDate })
            .Take(5)
            .ToListAsync();

        // جوائز هذا المتجر أولاً، ثم جوائز المول العامة
        var rewards = await _context.Rewards.AsNoTracking()
            .Where(r => r.IsActive && (r.ValidUntil == null || r.ValidUntil > now) && (r.StockQuantity == null || r.StockQuantity > 0))
            .ToListAsync();
        bool ForStore(string? merchant) => !string.IsNullOrWhiteSpace(merchant) &&
            (merchant.Contains(store.NameAr, StringComparison.OrdinalIgnoreCase) || store.NameAr.Contains(merchant, StringComparison.OrdinalIgnoreCase) ||
             (!string.IsNullOrWhiteSpace(store.NameEn) && (merchant.Contains(store.NameEn, StringComparison.OrdinalIgnoreCase) || store.NameEn.Contains(merchant, StringComparison.OrdinalIgnoreCase))));
        var relevant = rewards.Where(r => ForStore(r.MerchantName)).Select(r => (r, here: true))
            .Concat(rewards.Where(r => string.IsNullOrWhiteSpace(r.MerchantName)).Select(r => (r, here: false)))
            .ToList();

        // بيانات العميل (اختيارية)
        int? balance = null;
        object? mine = null;
        var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (User.Identity?.IsAuthenticated == true && customerId != null)
        {
            balance = (int)(await _context.CustomerWallets.AsNoTracking()
                .Where(w => w.CustomerId == customerId && w.BalanceType == BalanceType.POINTS)
                .Select(w => (decimal?)w.CurrentBalance).FirstOrDefaultAsync() ?? 0);
            var invoices = await _context.Invoices.AsNoTracking()
                .Where(i => i.CustomerId == customerId && i.TenantId == id)
                .Select(i => new { i.ProcessingStatus, i.TotalAmount, i.PointsAwarded, i.CreatedAt })
                .ToListAsync();
            var approved = invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).ToList();
            mine = new
            {
                invoicesCount = invoices.Count,
                approvedCount = approved.Count,
                totalSpent = approved.Sum(i => i.TotalAmount ?? 0),
                pointsEarned = approved.Sum(i => i.PointsAwarded),
                lastVisit = invoices.OrderByDescending(i => i.CreatedAt).Select(i => (DateTime?)i.CreatedAt).FirstOrDefault(),
            };
        }

        decimal? SpendNeeded(int points) => calc is "SPEND_RATIO" or "HYBRID" && ratio > 0 && points > 0
            ? Math.Ceiling(points / ratio)
            : null;

        var rewardItems = relevant
            .OrderBy(x => x.r.PointsCost)
            .Select(x =>
            {
                var remaining = balance.HasValue ? Math.Max(0, x.r.PointsCost - balance.Value) : x.r.PointsCost;
                return new
                {
                    id = x.r.Id,
                    title = x.r.Title,
                    description = x.r.Description,
                    imageUrl = x.r.ImageUrl,
                    pointsCost = x.r.PointsCost,
                    discountValue = x.r.DiscountValue,
                    atThisStore = x.here,
                    remainingPoints = remaining,
                    progress = x.r.PointsCost <= 0 ? 1.0 : Math.Min(1.0, (double)(balance ?? 0) / x.r.PointsCost),
                    canRedeem = balance.HasValue && remaining == 0,
                    estimatedSpend = SpendNeeded(remaining),
                };
            })
            .ToList();
        var next = rewardItems.FirstOrDefault(r => !r.canRedeem);

        return Ok(new
        {
            store = new
            {
                id = store.TenantId,
                nameAr = store.NameAr,
                nameEn = store.NameEn,
                category = store.Category,
                floor = store.FloorNumber,
                unitNumber = store.UnitNumber,
                logoUrl = store.LogoUrl,
                phone = store.ContactPhone,
            },
            howToEarn = earn,
            boosters,
            pointsBalance = balance,
            mine,
            nextReward = next,
            rewards = rewardItems,
        });
    }
}


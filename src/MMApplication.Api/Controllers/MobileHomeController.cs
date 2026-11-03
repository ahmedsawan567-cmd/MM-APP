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
}

namespace MMApplication.Application.DTOs;

public class MobileHomeResponseDto
{
    public UserProfileHomeDto? User { get; set; }
    public List<MobileBannerDto> HeroBanners { get; set; } = new();
    public List<MobileCategoryDto> QuickCategories { get; set; } = new();
    public List<MobileBoosterOfferDto> HotBoosters { get; set; } = new();
    public List<MobileStoreSpotlightDto> TopStores { get; set; } = new();
    public ServerDrivenUiMetaDto LayoutMeta { get; set; } = new();
}

public class UserProfileHomeDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string LoyaltyTier { get; set; } = "BRONZE";
    public int PointsBalance { get; set; }
    public decimal TierProgressPercentage { get; set; }
    public int PointsToNextTier { get; set; }
    public string NextTierName { get; set; } = "SILVER";
    public decimal TierMultiplier { get; set; } = 1.0m;
    /// <summary>اسم العضوية الحالية والتالية بالعربية ولونها ومزاياها (من إعدادات العضويات).</summary>
    public string LoyaltyTierName { get; set; } = string.Empty;
    public string? NextTierNameAr { get; set; }
    public string? TierColor { get; set; }
    public List<string> TierBenefits { get; set; } = new();
    public decimal SpendToNextTier { get; set; }
}

public class MobileBannerDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ActionType { get; set; } = "EXTERNAL_URL"; // OPEN_STORE, OPEN_CAMERA_BOOSTER, OPEN_COUPON, EXTERNAL_URL, OPEN_CATEGORY
    public string? ActionPayloadJson { get; set; }
    public string? TargetUrl { get; set; }
    public int DisplayOrder { get; set; }
}

public class MobileCategoryDto
{
    public string Key { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public int StoresCount { get; set; }
}

public class MobileBoosterOfferDto
{
    public Guid BoosterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public int BonusPoints { get; set; }
    public decimal PointsMultiplier { get; set; }
    public DateTime EndDate { get; set; }
    public int DaysRemaining { get; set; }
}

public class MobileStoreSpotlightDto
{
    public string TenantId { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public string FloorLabel { get; set; } = string.Empty;
}

public class ServerDrivenUiMetaDto
{
    public string AppVersionRequired { get; set; } = "1.0.0";
    public bool MaintenanceMode { get; set; } = false;
    public string ActiveTheme { get; set; } = "gold-dark";
    public List<string> EnabledSectionsOrder { get; set; } = new()
    {
        "USER_HEADER",
        "HERO_CAROUSEL",
        "QUICK_ACTIONS",
        "HOT_BOOSTERS",
        "CATEGORIES_GRID",
        "FEATURED_STORES"
    };
}

public class MobileStoreDto
{
    public string TenantId { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public string FloorLabel { get; set; } = string.Empty;
    public string RewardType { get; set; } = "POINTS";
    public decimal ConversionRate { get; set; }
}

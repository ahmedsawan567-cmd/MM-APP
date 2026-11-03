namespace MMApplication.Domain.Entities;

public class PromotionBanner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public string ActionType { get; set; } = "EXTERNAL_URL"; // OPEN_STORE, OPEN_CAMERA_BOOSTER, OPEN_COUPON, EXTERNAL_URL, OPEN_CATEGORY
    public string? ActionPayloadJson { get; set; }
    public string TargetAudience { get; set; } = "ALL"; // ALL, VIP, GOLD, PLATINUM, SILVER, BRONZE, INACTIVE_30D, NEW_USERS
    public int DisplayOrder { get; set; } = 1;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

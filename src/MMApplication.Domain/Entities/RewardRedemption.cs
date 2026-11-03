using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class RewardRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerId { get; set; } = string.Empty;
    public Guid RewardId { get; set; }
    public string RedemptionCode { get; set; } = string.Empty;
    public int PointsSpent { get; set; }
    public RedemptionStatus Status { get; set; } = RedemptionStatus.ACTIVE;
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
    public string? UsedByMerchant { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public Reward Reward { get; set; } = null!;
}

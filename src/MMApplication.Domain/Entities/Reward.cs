namespace MMApplication.Domain.Entities;

public class Reward
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public int PointsCost { get; set; }
    public decimal DiscountValue { get; set; }
    public string? ImageUrl { get; set; }
    public int? StockQuantity { get; set; } // null means unlimited
    public bool IsActive { get; set; } = true;
    public int ValidityDays { get; set; } = 30; // Voucher validity duration after redemption
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<RewardRedemption> Redemptions { get; set; } = new List<RewardRedemption>();
}

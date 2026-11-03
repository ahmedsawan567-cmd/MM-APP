namespace MMApplication.Domain.Entities;

public class TenantLoyaltyRule
{
    public int RuleId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RewardType { get; set; } = "POINTS"; // 'POINTS', 'CASHBACK'
    public decimal ConversionRate { get; set; } = 0.1000m; // 1 point per 10 LYD (0.1)
    public decimal MinInvoiceTotal { get; set; } = 0.00m;
    public int MaxPointsCap { get; set; } = 500;
    public int PointsExpiryDays { get; set; } = 180;
    public int MaxInvoiceAgeDays { get; set; } = 7;
    public string? DynamicRulesJson { get; set; } // JSON: weekend multipliers, item bonuses
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}

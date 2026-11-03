using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class StoreSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreTenantId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty; // E.g. "أكتوبر 2026 - الدورة 1"
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int RedemptionsCount { get; set; }
    public decimal TotalRedemptionsValue { get; set; }
    public decimal MallCommissionAmount { get; set; }
    public decimal NetPayableAmount { get; set; }
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    public DateTime? SettledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public StoreTenant StoreTenant { get; set; } = null!;
}

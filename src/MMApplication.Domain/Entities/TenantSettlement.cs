using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class TenantSettlement
{
    public Guid SettlementId { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public decimal TotalVouchersRedeemedValue { get; set; }
    public decimal MallServiceFee { get; set; } = 0.00m;
    public decimal NetPayableToTenant { get; set; }
    public SettlementStatus SettlementStatus { get; set; } = SettlementStatus.OPEN;
    public string? SettledByUserId { get; set; }
    public DateTime? SettledAt { get; set; }

    // Helper compatibility properties
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid Id { get => SettlementId; set => SettlementId = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid StoreTenantId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Tenant? StoreTenant { get => Tenant; set => Tenant = value!; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PeriodLabel { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int RedemptionsCount { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal TotalRedemptionsValue { get => TotalVouchersRedeemedValue; set => TotalVouchersRedeemedValue = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MallCommissionAmount { get => MallServiceFee; set => MallServiceFee = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal NetPayableAmount { get => NetPayableToTenant; set => NetPayableToTenant = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public SettlementStatus Status { get => SettlementStatus; set => SettlementStatus = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime PeriodStart { get => PeriodStartDate; set => PeriodStartDate = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime PeriodEnd { get => PeriodEndDate; set => PeriodEndDate = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public SystemUser? SettledByUser { get; set; }
}

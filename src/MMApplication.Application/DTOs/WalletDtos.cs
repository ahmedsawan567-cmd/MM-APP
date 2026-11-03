namespace MMApplication.Application.DTOs;

public class PointTransactionDto
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class VoucherDto
{
    public Guid Id { get; set; }
    public string RedemptionCode { get; set; } = string.Empty;
    public Guid RewardId { get; set; }
    public string RewardTitle { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public decimal DiscountValue { get; set; }
    public int PointsSpent { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RedeemedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByMerchant { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt && Status == "ACTIVE";
}

public class WalletSummaryDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int PointsBalance { get; set; }
    public int TotalPointsEarned { get; set; }
    public int TotalPointsSpent { get; set; }
    public int ActiveVouchersCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class UseVoucherRequestDto
{
    public string? MerchantName { get; set; }
}

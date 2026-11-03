using System.ComponentModel.DataAnnotations;

namespace MMApplication.Application.DTOs;

public class RewardDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public int PointsCost { get; set; }
    public decimal DiscountValue { get; set; }
    public string? ImageUrl { get; set; }
    public int? StockQuantity { get; set; }
    public bool IsActive { get; set; }
    public int ValidityDays { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateRewardDto
{
    [Required(ErrorMessage = "عنوان المكافأة مطلوب")]
    [StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "وصف المكافأة مطلوب")]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public string? MerchantName { get; set; }

    [Required(ErrorMessage = "تكلفة النقاط مطلوبة")]
    [Range(1, 1000000, ErrorMessage = "يجب أن تكون النقاط على الأقل 1")]
    public int PointsCost { get; set; }

    [Range(0, 1000000, ErrorMessage = "قيمة الخصم يجب أن تكون 0 أو أكثر")]
    public decimal DiscountValue { get; set; }

    public string? ImageUrl { get; set; }

    public int? StockQuantity { get; set; }

    [Range(1, 365)]
    public int ValidityDays { get; set; } = 30;

    public DateTime? ValidUntil { get; set; }
}

public class RedemptionResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid RedemptionId { get; set; }
    public string RedemptionCode { get; set; } = string.Empty;
    public Guid RewardId { get; set; }
    public string RewardTitle { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public decimal DiscountValue { get; set; }
    public int PointsSpent { get; set; }
    public int CurrentWalletPointsBalance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RedeemedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

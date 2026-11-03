using System.ComponentModel.DataAnnotations;

namespace MMApplication.Application.DTOs;

public class DashboardMetricsDto
{
    public int TotalCustomers { get; set; }
    public int TotalInvoices { get; set; }
    public int ApprovedInvoicesCount { get; set; }
    public int PendingInvoicesCount { get; set; }
    public int RejectedInvoicesCount { get; set; }
    public decimal TotalSalesVolume { get; set; }
    public int TotalPointsIssued { get; set; }
    public int TotalPointsRedeemed { get; set; }
    public int ActiveVouchersCount { get; set; }
    public int TotalRewardsCount { get; set; }
}

public class TopMerchantDto
{
    public string MerchantName { get; set; } = string.Empty;
    public int InvoicesCount { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalPointsAwarded { get; set; }
}

public class RecentActivityDto
{
    public string ActivityType { get; set; } = string.Empty; // INVOICE, REDEMPTION, REGISTRATION
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal? Amount { get; set; }
    public int? Points { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AdjustPointsDto
{
    [Required(ErrorMessage = "عدد النقاط مطلوب")]
    public int PointsAmount { get; set; } // Positive to add, negative to deduct

    [Required(ErrorMessage = "سبب تعديل النقاط مطلوب")]
    [StringLength(300, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public class RejectInvoiceRequestDto
{
    [Required(ErrorMessage = "سبب رفض الفاتورة مطلوب")]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public class AdminCustomerDetailsDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int PointsBalance { get; set; }
    public int InvoicesCount { get; set; }
    public int RedemptionsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

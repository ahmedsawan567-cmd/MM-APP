using MMApplication.Domain.Enums;

namespace MMApplication.Api.ViewModels;

/// <summary>ملف العميل الكامل في المنصة.</summary>
public class CustomerProfileViewModel
{
    public string CustomerId { get; set; } = string.Empty;
    public string? MembershipNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string Tier { get; set; } = "BRONZE";
    public bool IsFrozen { get; set; }
    public bool IsArchived { get; set; }
    public bool HasAppPassword { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastInvoiceAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int LoginCount { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? LastAppVersion { get; set; }
    public string? LastPlatform { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public string? PasswordChangedBy { get; set; }
    public List<AuditRow> PasswordHistory { get; set; } = new();

    public int PointsBalance { get; set; }
    public int PointsEarned { get; set; }
    public int PointsRedeemed { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AverageInvoice { get; set; }
    public int InvoicesCount { get; set; }
    public int ApprovedCount { get; set; }
    public int PendingCount { get; set; }
    public int RejectedCount { get; set; }
    public int VouchersActive { get; set; }
    public int SharedDeviceAccounts { get; set; }

    public bool CanEdit { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool CanDelete { get; set; }

    public List<InvoiceRow> Invoices { get; set; } = new();
    public List<LedgerRow> Ledger { get; set; } = new();
    public List<VoucherRow> Vouchers { get; set; } = new();
    public List<StoreRow> Stores { get; set; } = new();
    public List<NotificationRow> Notifications { get; set; } = new();
    public List<DeviceRow> Devices { get; set; } = new();
    public List<AuditRow> Audit { get; set; } = new();

    public record InvoiceRow(Guid Id, string Store, string? InvoiceNumber, DateTime Date, DateTime UploadedAt, decimal Amount, int Points,
        InvoiceStatus Status, int RiskScore, string? Reason, string? ImageUrl);
    public record LedgerRow(DateTime Date, WalletTransactionType Type, decimal Amount, decimal BalanceAfter, string? Remarks, Guid? InvoiceId);
    public record VoucherRow(string Code, string Title, int Points, RedemptionStatus Status, DateTime RedeemedAt, DateTime ExpiresAt, DateTime? UsedAt, string? UsedBy);
    public record StoreRow(string Store, string Category, int Visits, decimal Spent, int Points, DateTime LastVisit);
    public record NotificationRow(string Title, string Body, string Type, DateTime CreatedAt, bool IsRead);
    public record DeviceRow(string? Model, string? Os, DateTime FirstSeen, DateTime LastSeen, int OtherAccounts);
    public record AuditRow(DateTime Date, string By, string Action, string Details);
}

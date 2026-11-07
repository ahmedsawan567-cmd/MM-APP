using MMApplication.Application.DTOs;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Api.ViewModels;

/// <summary>
/// نموذج عرض صفحة طاقم العمل وتوزيع الصلاحيات
/// </summary>
public class StaffPageViewModel
{
    public List<StaffUserDto> StaffMembers { get; set; } = new();
    public List<StoreTenantDto> Stores { get; set; } = new();
    /// <summary>صلاحيات كل دور قابل للتعديل (FRAUD_AUDITOR، STORE_MANAGER، CASHIER).</summary>
    public Dictionary<string, HashSet<string>> RolePermissions { get; set; } = new();
    public bool CanEditPermissions { get; set; }
    public bool ActorIsSuperAdmin { get; set; }
}

/// <summary>
/// نموذج عرض صفحة محددات الذكاء الاصطناعي وسجلات الأمان
/// </summary>
public class SettingsPageViewModel
{
    public SystemSettingsDto Settings { get; set; } = new();
    public List<AuditLogDto> AuditLogs { get; set; } = new();
    public string? SearchQuery { get; set; }
    public string Tab { get; set; } = "rules";
    public string? ActionFilter { get; set; }
    public string? ByFilter { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalLogs { get; set; }
    public List<string> Performers { get; set; } = new();
    public List<string> ActionsInLog { get; set; } = new();
    public SystemStatusInfo Status { get; set; } = new();
}

/// <summary>حالة مكونات المنظومة (للاطلاع فقط).</summary>
public class SystemStatusInfo
{
    public string Database { get; set; } = "";
    public bool DatabaseIsReal { get; set; }
    public string AiProvider { get; set; } = "";
    public bool AiIsReal { get; set; }
    public string? AiModel { get; set; }
    public string? AiFallbackModel { get; set; }
    public int AiRetryQueue { get; set; }
    public int ReviewQueue { get; set; }
    public bool PushEnabled { get; set; }
    public int PushDevices { get; set; }
    public int Pushed24h { get; set; }
    public int Customers { get; set; }
    public int Stores { get; set; }
    public int Invoices { get; set; }
    public int Invoices24h { get; set; }
    public int Staff { get; set; }
    public int LockedStaff { get; set; }
    public int FailedLogins24h { get; set; }
    public string AppVersion { get; set; } = "";
    public DateTime ServerTime { get; set; }
}

/// <summary>
/// نموذج عرض مركز فحص الاحتيال والمطابقة
/// </summary>
public class FraudPageViewModel
{
    public List<FraudInvoiceItemDto> QueueItems { get; set; } = new();
    public string StatusFilter { get; set; } = "ALL";
    public int HighRiskCount { get; set; }
    public int SuspectCount { get; set; }
    public int DisputedCount { get; set; }
    public int ResubmitCount { get; set; }
    public int TotalInspected { get; set; }
    public decimal SystemAccuracyPercentage { get; set; } = 98.4m;
}

/// <summary>
/// نموذج عرض برنامج الولاء وحملات المواسم
/// </summary>
public class LoyaltyPageViewModel
{
    public List<LoyaltyCampaignDto> Campaigns { get; set; } = new();
    public List<ProductBoosterRuleDto> Boosters { get; set; } = new();
    public List<LoyaltyEarningRuleDto> EarningRules { get; set; } = new();
    public List<StoreTenantDto> Stores { get; set; } = new();
    public LoyaltyRulesDto Rules { get; set; } = new();
}

/// <summary>
/// كشف حساب وتفاصيل العميل الشاملة (فواتير، نقاط، محلات، ومكافآت)
/// </summary>
public class CustomerDetailsPageViewModel
{
    public string CustomerId { get; set; } = string.Empty;
    public string? MembershipNumber { get; set; }
    public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Tier { get; set; } = "Bronze";
    public int PointsBalance { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalCashBackEarned { get; set; }
    public int InvoicesCount { get; set; }
    public int ApprovedCount { get; set; }
    public int PendingCount { get; set; }
    public int RejectedCount { get; set; }
    public bool IsSuspended { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastActivityDate { get; set; }

    public List<CustomerInvoiceDetailDto> Invoices { get; set; } = new();
    public List<CustomerStoreVisitDto> StoreVisits { get; set; } = new();
    public List<CustomerRewardRedemptionDto> Redemptions { get; set; } = new();
}

public class CustomerInvoiceDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string StoreCategory { get; set; } = string.Empty;
    public string FloorNumber { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime ScanDateTime { get; set; }
    public decimal TotalAmount { get; set; }
    public int PointsEarned { get; set; }
    public decimal CashBackEarned { get; set; }
    public int RiskScore { get; set; }
    public InvoiceStatus Status { get; set; }
    public string? FraudFlagReason { get; set; }
    public decimal OcrConfidence { get; set; }
    public string? ImageUrl { get; set; }
}

public class CustomerStoreVisitDto
{
    public string StoreName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public decimal TotalSpent { get; set; }
    public int TotalPointsEarned { get; set; }
    public DateTime LastVisitDate { get; set; }
    public bool IsFavorite { get; set; }
}

public class CustomerRewardRedemptionDto
{
    public string Id { get; set; } = string.Empty;
    public string RewardTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int PointsSpent { get; set; }
    public decimal MonetaryValue { get; set; }
    public DateTime RedemptionDate { get; set; }
    public string Status { get; set; } = "Completed";
    public string RedemptionCode { get; set; } = string.Empty;
}

/// <summary>
/// نموذج تسجيل الدخول للوحة التحكم الإدارية
/// </summary>
public class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>صفحة نماذج الفواتير: بصمة فاتورة كل متجر كما تعلّمها النظام.</summary>
public class InvoiceTemplatesPageViewModel
{
    public List<StoreInvoiceProfileRow> Stores { get; set; } = new();
    public bool AiEnabled { get; set; }
    public int WithProfile => Stores.Count(s => s.HasProfile);
    public int DuplicatesBlocked30d { get; set; }
}

public class StoreInvoiceProfileRow
{
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string? StoreNameEn { get; set; }
    public string Category { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public bool StoreActive { get; set; }
    public List<string> Aliases { get; set; } = new();
    public int InvoicesCount { get; set; }
    public int ApprovedCount { get; set; }

    public bool HasProfile { get; set; }
    public bool ProfileActive { get; set; }
    public string? SampleImageUrl { get; set; }
    public string? PrintedName { get; set; }
    public string? TaxNumber { get; set; }
    public string? PhoneOnReceipt { get; set; }
    public string? InvoiceNumberLabel { get; set; }
    public string? InvoiceNumberExample { get; set; }
    public string? InvoiceNumberPattern { get; set; }
    public string? TotalLabel { get; set; }
    public string? DateFormat { get; set; }
    public string? Currency { get; set; }
    public string? LayoutNotes { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class SaveInvoiceProfileRequest
{
    public string StoreId { get; set; } = string.Empty;
    public string? SampleImageUrl { get; set; }
    public string? PrintedName { get; set; }
    /// <summary>أسماء أخرى تظهر على الفواتير، مفصولة بفواصل أو أسطر.</summary>
    public string? OtherNames { get; set; }
    public string? TaxNumber { get; set; }
    public string? PhoneOnReceipt { get; set; }
    public string? InvoiceNumberLabel { get; set; }
    public string? InvoiceNumberExample { get; set; }
    public string? InvoiceNumberPattern { get; set; }
    public string? TotalLabel { get; set; }
    public string? DateFormat { get; set; }
    public string? Currency { get; set; }
    public string? LayoutNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool Learned { get; set; }
}

public class StoreIdRequest
{
    public string StoreId { get; set; } = string.Empty;
}

public class ExtractedLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

/// <summary>
/// نموذج صفحة سجل الفواتير الشامل
/// </summary>
public class InvoicesPageViewModel
{
    public List<InvoiceItemViewModel> Invoices { get; set; } = new();
    public List<StoreTenantDto> Stores { get; set; } = new();
    public string SelectedStoreId { get; set; } = "ALL";
    public string StatusFilter { get; set; } = "ALL";
    public string SearchQuery { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public int TotalApprovedPoints { get; set; }
    public int PendingReviewsCount { get; set; }
    public int RejectedCount { get; set; }
}

/// <summary>
/// بند الفاتورة في جدول سجل الفواتير
/// </summary>
public class InvoiceItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string StoreCategory { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int PointsEarned { get; set; }
    public decimal CashbackEarned { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public InvoiceStatus Status { get; set; }
    public decimal ConfidenceScore { get; set; }
    public int RiskScore { get; set; }
    public string? ImageUrl { get; set; }
    public string? RejectionReason { get; set; }
    public List<ExtractedLineItemDto> LineItems { get; set; } = new();
}



// ---------------- مراجعة الفواتير ----------------
public class ReviewPageViewModel
{
    public string Queue { get; set; } = "open";
    public int OpenCount { get; set; }
    public int WaitingCount { get; set; }
    public string AiProvider { get; set; } = "-";
    public List<ReviewQueueItem> Items { get; set; } = new();
    public List<ReviewStoreOption> Stores { get; set; } = new();
}

public class ReviewQueueItem
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal? Confidence { get; set; }
    public List<string> Reasons { get; set; } = new();
}

public class ReviewStoreOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ReviewDecisionRequest
{
    public string? Reason { get; set; }
}

// ---------------- كتالوج المكافآت ----------------
public class RewardsPageViewModel
{
    public bool CanManage { get; set; }
    public bool CanSeeVouchers { get; set; }
    public List<RewardAdminRow> Rewards { get; set; } = new();
    public List<VoucherAdminRow> Vouchers { get; set; } = new();
    public List<string> StoreNames { get; set; } = new();
    public int IssuedTotal { get; set; }
    public int Issued30 { get; set; }
    public int UsedTotal { get; set; }
    public int ActiveNow { get; set; }
    public int ExpiringSoon { get; set; }
    public int PointsRedeemed { get; set; }
    public decimal DiscountGiven { get; set; }
}

public record VoucherAdminRow(string Code, string RewardTitle, string? MerchantName, string CustomerId, string CustomerName, string? MembershipNumber,
    int Points, decimal Discount, MMApplication.Domain.Enums.RedemptionStatus Status, DateTime RedeemedAt, DateTime ExpiresAt, DateTime? UsedAt, string? UsedBy);

public class RewardAdminRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public int PointsCost { get; set; }
    public decimal DiscountValue { get; set; }
    public string? ImageUrl { get; set; }
    public int? StockQuantity { get; set; }
    public int ValidityDays { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsActive { get; set; }
    public int Redemptions { get; set; }
    public int ActiveVouchers { get; set; }
    public int UsedVouchers { get; set; }
    public int PointsSpent { get; set; }
}

public class RewardEditRequest
{
    public Guid? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? MerchantName { get; set; }
    public int PointsCost { get; set; }
    public decimal DiscountValue { get; set; }
    public string? ImageUrl { get; set; }
    public int? StockQuantity { get; set; }
    public int ValidityDays { get; set; } = 30;
    public DateTime? ValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TiersPageViewModel
{
    public List<TierRow> Tiers { get; set; } = new();
    public int TotalCustomers { get; set; }
    public int LockedCustomers { get; set; }
    public int UpgradedLast30 { get; set; }
    public bool CanManage { get; set; }
}

public record TierRow(MMApplication.Domain.Entities.MembershipTier Tier, int Members, decimal AvgSpend, int AvgPoints);

public class TierSaveRequest
{
    public string? OriginalCode { get; set; }
    public string? Code { get; set; }
    public string? NameAr { get; set; }
    public string? Benefits { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public int MinPoints { get; set; }
    public decimal MinSpend { get; set; }
    public int QualificationMonths { get; set; }
    public decimal PointsMultiplier { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
}

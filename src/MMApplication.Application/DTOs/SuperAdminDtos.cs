using MMApplication.Domain.Enums;

namespace MMApplication.Application.DTOs;

public class SuperAdminOverviewDto
{
    public decimal TotalSalesVolume { get; set; }
    public int TodayScansCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int ActiveCustomersCount { get; set; }
    public int CirculatingPoints { get; set; }
    public decimal AiConfidenceAverage { get; set; }
    public string SystemStatus { get; set; } = "Operational 100%";
    public List<ScanChartDataPoint> ScanActivityPace { get; set; } = new();
    public List<CategorySpendPoint> CategoryDistribution { get; set; } = new();
    public List<TopStorePoint> TopPerformingStores { get; set; } = new();
}

public class ScanChartDataPoint
{
    public string TimeLabel { get; set; } = string.Empty;
    public int ScanCount { get; set; }
    public decimal VolumeAmount { get; set; }
}

public class CategorySpendPoint
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
}

public class TopStorePoint
{
    public string StoreName { get; set; } = string.Empty;
    public int InvoicesCount { get; set; }
    public decimal TotalSales { get; set; }
}

public class StoreTenantDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FloorNumber { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public StoreLoyaltyType LoyaltyType { get; set; }
    public string LoyaltyTypeLabel { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
    public int ActiveStaffCount { get; set; }
    public decimal TotalSales { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<StoreAliasDto> Aliases { get; set; } = new();
}

public class CreateStoreTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FloorNumber { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public StoreLoyaltyType LoyaltyType { get; set; } = StoreLoyaltyType.MallPoints;
    public decimal CommissionRate { get; set; } = 5.00m;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? LogoUrl { get; set; }
}

public class UpdateStoreTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FloorNumber { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public StoreLoyaltyType LoyaltyType { get; set; }
    public decimal CommissionRate { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
}

public class StoreAliasDto
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string AliasName { get; set; } = string.Empty;
    public string MatchType { get; set; } = "CONTAINS";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class CreateStoreAliasRequest
{
    public string AliasName { get; set; } = string.Empty;
    public string MatchType { get; set; } = "CONTAINS";
}

public class FraudInvoiceItemDto
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int PointsEarned { get; set; }
    public InvoiceStatus Status { get; set; }
    public int RiskScore { get; set; }
    public string? PhotoHash { get; set; }
    public string? FraudFlagReason { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasHashMatch { get; set; }
    public int HashMatchSimilarity { get; set; }

    // Dispute & Re-take fields
    public string? ResubmissionNotes { get; set; }
    public string? DisputeReason { get; set; }
    public bool IsDisputed => Status == InvoiceStatus.DISPUTED;
    public bool IsResubmissionRequested => Status == InvoiceStatus.RESUBMISSION_REQUESTED;

    // Device & Anti-Sybil fields
    public string? DeviceFingerprint { get; set; }
    public bool HasMultiAccountFlag { get; set; }
    public int SharedAccountsCount { get; set; }
    public bool IsMultiAccountDevice => HasMultiAccountFlag || SharedAccountsCount > 1;
    public List<string> SharedAccountNames { get; set; } = new();
}

public class ReviewInvoiceRequest
{
    public string Action { get; set; } = string.Empty; // "APPROVE", "REJECT", "FLAG_FRAUD", "FREEZE_USER", "REQUEST_RESCAN"
    public string? Reason { get; set; }
}

public class RequestRescanRequest
{
    public string ResubmissionNotes { get; set; } = string.Empty;
}

public class CustomerDisputeRequest
{
    public string DisputeReason { get; set; } = string.Empty;
}

public class PhotoHashComparisonResultDto
{
    public Guid CurrentInvoiceId { get; set; }
    public string CurrentPhotoHash { get; set; } = string.Empty;
    public string? CurrentImageUrl { get; set; }
    public List<MatchingPhotoDto> SimilarInvoices { get; set; } = new();
}

public class MatchingPhotoDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string PhotoHash { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int SimilarityPercentage { get; set; }
    public DateTime ScannedAt { get; set; }
}

public class PromotionBannerDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public string ActionType { get; set; } = "EXTERNAL_URL";
    public string? ActionPayloadJson { get; set; }
    public string TargetAudience { get; set; } = "ALL";
    public int DisplayOrder { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePromotionBannerRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public string ActionType { get; set; } = "EXTERNAL_URL";
    public string? ActionPayloadJson { get; set; }
    public string TargetAudience { get; set; } = "ALL";
    public int DisplayOrder { get; set; } = 1;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class SendPushNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = "ALL"; // ALL, VIP_GOLD, SILVER, INACTIVE_30D
    public string? DeepLink { get; set; }
}

public class LoyaltyCampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PointsMultiplier { get; set; }
    public decimal MinSpendAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateLoyaltyCampaignRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PointsMultiplier { get; set; } = 2.00m;
    public decimal MinSpendAmount { get; set; } = 0.00m;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class ProductBoosterRuleDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BrandOrItemKeyword { get; set; } = string.Empty;
    public string? TargetCategory { get; set; }
    public string? TenantId { get; set; }
    public string? StoreName { get; set; }
    public int BonusPoints { get; set; }
    public decimal PointsMultiplier { get; set; }
    public string? SponsorName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Convenience view aliases
    public string RuleName => Title;
    public string TargetValue => BrandOrItemKeyword;
    public string TargetType => TargetCategory ?? "Brand";
    public int MinQuantity { get; set; } = 1;
    public decimal? MinSpendAmount { get; set; } = 0m;
}

public class CreateProductBoosterRuleRequest
{
    public string Title { get; set; } = string.Empty;
    public string BrandOrItemKeyword { get; set; } = string.Empty;
    public string? TargetCategory { get; set; }
    public string? TenantId { get; set; }
    public int BonusPoints { get; set; } = 20;
    public decimal PointsMultiplier { get; set; } = 1.00m;
    public string? SponsorName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class UpdateProductBoosterRuleRequest
{
    public string Title { get; set; } = string.Empty;
    public string BrandOrItemKeyword { get; set; } = string.Empty;
    public string? TargetCategory { get; set; }
    public string? TenantId { get; set; }
    public int BonusPoints { get; set; } = 20;
    public decimal PointsMultiplier { get; set; } = 1.00m;
    public string? SponsorName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LoyaltyRulesDto
{
    public decimal DinarsPerPoint { get; set; } = 10.00m; // 1 pt per 10 LYD
    public decimal PointRedemptionValueDinars { get; set; } = 0.50m; // 1 pt = 0.50 LYD
    public int BronzeThreshold { get; set; } = 0;
    public int SilverThreshold { get; set; } = 500;
    public int GoldThreshold { get; set; } = 1500;
    public int PlatinumThreshold { get; set; } = 5000;
    public decimal SilverMultiplier { get; set; } = 1.25m;
    public decimal GoldMultiplier { get; set; } = 1.50m;
    public decimal PlatinumMultiplier { get; set; } = 2.00m;
}

public class StoreSettlementDto
{
    public Guid Id { get; set; }
    public string StoreTenantId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int RedemptionsCount { get; set; }
    public decimal TotalRedemptionsValue { get; set; }
    public decimal MallCommissionAmount { get; set; }
    public decimal NetPayableAmount { get; set; }
    public SettlementStatus Status { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GenerateSettlementBatchRequest
{
    public string? PeriodLabel { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}

public class SuperAdminCustomerDto
{
    public string Id { get; set; } = string.Empty;
    public string? MembershipNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public bool HasAppAccess { get; set; }
    public string Tier { get; set; } = "Bronze";
    public int CurrentPoints { get; set; }
    public int TotalInvoicesScanned { get; set; }
    public decimal TotalSpent { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsArchived { get; set; }
    public string? DeviceFingerprint { get; set; }
    public bool HasMultiAccountDeviceFlag { get; set; }
    public int SharedDeviceAccountsCount { get; set; }
    public bool IsMultiAccountDevice => HasMultiAccountDeviceFlag || SharedDeviceAccountsCount > 1;
    public int SharedDevicesCount => SharedDeviceAccountsCount;
    public DateTime CreatedAt { get; set; }
}

public class CustomerDeviceDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public class AdjustCustomerPointsRequest
{
    public int PointsDelta { get; set; } // +100 or -50
    public string Reason { get; set; } = string.Empty;
}

public class StaffUserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public CustomerRole Role { get; set; }
    public StaffRole? StaffRole { get; set; }
    public string StaffRoleLabel { get; set; } = string.Empty;
    public string? StoreTenantId { get; set; }
    public string? StoreName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsCurrentUser { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStaffUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public CustomerRole Role { get; set; } = CustomerRole.Admin;
    public StaffRole StaffRole { get; set; } = StaffRole.Auditor;
    public string? StoreTenantId { get; set; }
}

public class SystemSettingsDto
{
    public decimal OcrConfidenceThreshold { get; set; } = 85.00m;
    public int DuplicateHashToleranceDistance { get; set; } = 5;
    public int HighRiskScoreThreshold { get; set; } = 70;
    public int MediumRiskScoreThreshold { get; set; } = 30;
    public int MaxDailyScansPerCustomer { get; set; } = 10;
    public decimal MaxInvoiceAmountAutoApproval { get; set; } = 5000.00m;
    public bool EnableAutomaticFraudDetection { get; set; } = true;
    public string ActiveOcrProvider { get; set; } = "Mock Receipt Engine (Azure/OpenAI Ready)";
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCustomerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Tier { get; set; } = "BRONZE";
    public int InitialPoints { get; set; } = 0;
    public string? DeviceFingerprint { get; set; }
    public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    /// <summary>كلمة مرور للتطبيق (اختيارية). بدونها لا يستطيع العميل الدخول للتطبيق حتى تُضبط.</summary>
    public string? Password { get; set; }
}

public class UpdateCustomerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Tier { get; set; } = "BRONZE";
    public bool? IsFrozen { get; set; }
    public bool? IsArchived { get; set; }
    public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
}

public class ResetCustomerPasswordRequest
{
    /// <summary>فارغة = توليد كلمة مرور عشوائية تُعرض مرة واحدة.</summary>
    public string? NewPassword { get; set; }
}

public class UpdateStaffUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public StaffRole StaffRole { get; set; } = StaffRole.Auditor;
    public string? StoreTenantId { get; set; }
    public string? Password { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdatePromotionBannerRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public string ActionType { get; set; } = "EXTERNAL_URL";
    public string? ActionPayloadJson { get; set; }
    public string TargetAudience { get; set; } = "ALL";
    public int DisplayOrder { get; set; } = 1;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateLoyaltyCampaignRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PointsMultiplier { get; set; } = 2.00m;
    public decimal MinSpendAmount { get; set; } = 0.00m;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateManualInvoiceRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? MerchantName { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ReceiptImageUrl { get; set; }
}

public class UpdateInvoiceRequest
{
    public string? TenantId { get; set; }
    public string? MerchantName { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal? TotalAmount { get; set; }
    public InvoiceStatus? ProcessingStatus { get; set; }
    public string? RejectionReason { get; set; }
    public string? ResubmissionNotes { get; set; }
}

public class LoyaltyEarningRuleDto
{
    public int RuleId { get; set; }
    public string? StoreTenantId { get; set; }
    public string? StoreName { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string CalculationType { get; set; } = "SPEND_RATIO"; // FIXED_PER_INVOICE, SPEND_RATIO, ITEM_BOOSTER, HYBRID
    public int FixedPointsPerInvoice { get; set; }
    public decimal SpendRatioRate { get; set; }
    public decimal MinInvoiceAmount { get; set; }
    public int? MaxPointsCap { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateLoyaltyEarningRuleRequest
{
    public string? StoreTenantId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string CalculationType { get; set; } = "SPEND_RATIO";
    public int FixedPointsPerInvoice { get; set; } = 0;
    public decimal SpendRatioRate { get; set; } = 0.1000m;
    public decimal MinInvoiceAmount { get; set; } = 0.00m;
    public int? MaxPointsCap { get; set; }
}

public class UpdateLoyaltyEarningRuleRequest
{
    public string? StoreTenantId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string CalculationType { get; set; } = "SPEND_RATIO";
    public int FixedPointsPerInvoice { get; set; } = 0;
    public decimal SpendRatioRate { get; set; } = 0.1000m;
    public decimal MinInvoiceAmount { get; set; } = 0.00m;
    public int? MaxPointsCap { get; set; }
    public bool IsActive { get; set; } = true;
}


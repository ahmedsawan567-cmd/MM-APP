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
}

/// <summary>
/// نموذج عرض صفحة محددات الذكاء الاصطناعي وسجلات الأمان
/// </summary>
public class SettingsPageViewModel
{
    public SystemSettingsDto Settings { get; set; } = new();
    public List<AuditLogDto> AuditLogs { get; set; } = new();
    public string? SearchQuery { get; set; }
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

/// <summary>
/// نموذج عرض صفحة نماذج الفواتير والتعرف الذكي بالذكاء الاصطناعي
/// </summary>
public class InvoiceTemplatesPageViewModel
{
    public List<StoreInvoiceTemplateDto> Templates { get; set; } = new();
    public List<StoreTenantDto> Stores { get; set; } = new();
    public int TotalTemplatesCount => Templates.Count;
    public int ActiveTemplatesCount => Templates.Count(t => t.IsActive);
    public decimal AverageOcrAccuracy => Templates.Any() ? Math.Round(Templates.Average(t => t.RecognitionAccuracy), 1) : 98.5m;
    public string? SelectedStoreId { get; set; }
}

/// <summary>
/// بيانات نموذج فاتورة المتجر للتعرف الذكي
/// </summary>
public class StoreInvoiceTemplateDto
{
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "شريط كاشير حراري POS";
    public List<string> HeaderAliases { get; set; } = new();
    public string TotalAmountKeyword { get; set; } = "المجموع الكلي";
    public string InvoiceNumberPrefix { get; set; } = "INV-";
    public string DateFormatKeyword { get; set; } = "التاريخ";
    public string TaxNumber { get; set; } = string.Empty;
    public string? SampleImageUrl { get; set; }
    public decimal RecognitionAccuracy { get; set; } = 98.5m;
    public int TotalRecognizedInvoices { get; set; }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// طلب حفظ أو تحديث نموذج فاتورة لمتجر
/// </summary>
public class SaveInvoiceTemplateRequest
{
    public string StoreId { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "شريط كاشير حراري POS";
    public string HeaderAliases { get; set; } = string.Empty; // أسماء مفصولة بفواصل
    public string TotalAmountKeyword { get; set; } = "المجموع الكلي";
    public string InvoiceNumberPrefix { get; set; } = "INV-";
    public string DateFormatKeyword { get; set; } = "التاريخ";
    public string TaxNumber { get; set; } = string.Empty;
    public string? SampleImageUrl { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// طلب اختبار فحص فاتورة بالذكاء الاصطناعي
/// </summary>
public class AiInvoiceScanTestRequest
{
    public string? StoreId { get; set; }
    public string? ReceiptText { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>
/// نتيجة اختبار فحص فاتورة بالذكاء الاصطناعي
/// </summary>
public class AiInvoiceScanTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RecognizedStoreId { get; set; }
    public string RecognizedStoreName { get; set; } = string.Empty;
    public string ExtractedInvoiceNumber { get; set; } = string.Empty;
    public decimal ExtractedTotalAmount { get; set; }
    public DateTime ExtractedDate { get; set; }
    public decimal ConfidencePercentage { get; set; }
    public string MatchedTemplateType { get; set; } = string.Empty;
    public List<ExtractedLineItemDto> LineItems { get; set; } = new();
    public int CalculatedPoints { get; set; }
    public decimal CalculatedCashback { get; set; }
    public bool IsFraudRiskDetected { get; set; }
    public string? FraudRiskReason { get; set; }
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

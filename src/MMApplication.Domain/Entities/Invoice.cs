using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class Invoice
{
    public Guid InvoiceId { get; set; } = Guid.NewGuid();
    public string CustomerId { get; set; } = string.Empty;
    public string? TenantId { get; set; }

    // AI extraction data
    public string? ExtractedMerchant { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? ConfidenceScore { get; set; } // 0.0000 to 1.0000
    public string? RawAiPayload { get; set; }   // Full raw AI JSON payload

    // Anti-fraud fingerprints
    public string ReceiptImageUrl { get; set; } = string.Empty;
    public string? ImagePHash { get; set; }      // Perceptual hash
    public int FraudRiskScore { get; set; } = 0; // 0 to 100

    // Lifecycle processing status
    public InvoiceStatus ProcessingStatus { get; set; } = InvoiceStatus.PENDING;
    public string? RejectionReason { get; set; }

    // Re-take and Dispute tracking
    public string? ResubmissionNotes { get; set; }
    public DateTime? ResubmissionRequestedAt { get; set; }
    public string? DisputeReason { get; set; }
    public DateTime? DisputedAt { get; set; }

    public int PointsAwarded { get; set; } = 0;
    public int BasePointsEarned { get; set; } = 0;
    public int ItemBonusPointsEarned { get; set; } = 0;
    public string? PointsBreakdownJson { get; set; }
    public decimal CashbackAwarded { get; set; } = 0.00m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper properties for backward compatibility
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid Id { get => InvoiceId; set => InvoiceId = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string MerchantName { get => ExtractedMerchant ?? string.Empty; set => ExtractedMerchant = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? ImageUrl { get => ReceiptImageUrl; set => ReceiptImageUrl = value ?? string.Empty; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int RiskScore { get => FraudRiskScore; set => FraudRiskScore = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PhotoHash { get => ImagePHash; set => ImagePHash = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public InvoiceStatus Status { get => ProcessingStatus; set => ProcessingStatus = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int PointsEarned { get => PointsAwarded; set => PointsAwarded = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? FraudFlagReason { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public Tenant? Tenant { get; set; }
    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<WalletLedger> LedgerEntries { get; set; } = new List<WalletLedger>();
    public ICollection<FraudReviewLog> FraudReviews { get; set; } = new List<FraudReviewLog>();
}

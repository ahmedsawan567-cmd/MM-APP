namespace MMApplication.Domain.Entities;

public class FraudReviewLog
{
    public long ReviewId { get; set; }
    public Guid InvoiceId { get; set; }
    public string? AssignedAuditorId { get; set; }
    public int CalculatedRiskScore { get; set; }
    public string TriggeredFlags { get; set; } = "[]"; // JSON Array: ["SCREEN_DETECTED", "VELOCITY_EXCEEDED"]
    public string? AuditorDecision { get; set; }       // 'APPROVED', 'REJECTED'
    public string? DecisionReason { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Invoice Invoice { get; set; } = null!;
    public SystemUser? AssignedAuditor { get; set; }
}

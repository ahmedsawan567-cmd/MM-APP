namespace MMApplication.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PerformedBy { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // E.g. "INVOICE_APPROVE", "POINTS_ADJUST", "STORE_ADD"
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

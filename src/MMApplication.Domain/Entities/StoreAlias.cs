namespace MMApplication.Domain.Entities;

/// <summary>
/// جدول ترويسات وأسماء المتاجر البديلة لمطابقة الـ OCR الآلية
/// </summary>
public class StoreAlias
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string AliasName { get; set; } = string.Empty; // e.g. "Caffe Latte", "كافيه لاتيه", "شركة الأفق للمشروبات"
    public string MatchType { get; set; } = "CONTAINS";   // EXACT, CONTAINS, REGEX
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}

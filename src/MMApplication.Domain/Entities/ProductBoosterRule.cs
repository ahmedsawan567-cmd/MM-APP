namespace MMApplication.Domain.Entities;

/// <summary>
/// قواعد محفزات المنتجات والعلامات التجارية وسلة المشتريات (Brand & SKU Booster Rules)
/// </summary>
public class ProductBoosterRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;              // e.g. "بونص عصير المراعي 1 لتر"
    public string BrandOrItemKeyword { get; set; } = string.Empty; // e.g. "المراعي", "Almarai", "Kinder"
    public string? TargetCategory { get; set; }                    // Supermarket, Cafe, etc.
    public string? TenantId { get; set; }                          // specific store or mall-wide if null
    public int BonusPoints { get; set; } = 20;                     // +20 flat bonus points
    public decimal PointsMultiplier { get; set; } = 1.00m;         // optional multiplier for the item
    public string? SponsorName { get; set; }                       // e.g. "شركة المراعي للتوزيع"
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant? Tenant { get; set; }
}

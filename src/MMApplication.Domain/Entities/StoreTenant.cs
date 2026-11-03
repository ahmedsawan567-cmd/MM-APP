using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class StoreTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FloorNumber { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public StoreLoyaltyType LoyaltyType { get; set; } = StoreLoyaltyType.MallPoints;
    public decimal CommissionRate { get; set; } = 5.00m; // E.g. 5%
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigations
    public ICollection<StoreSettlement> Settlements { get; set; } = new List<StoreSettlement>();
}

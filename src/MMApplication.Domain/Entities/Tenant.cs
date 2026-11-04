using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class Tenant
{
    public string TenantId { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty; // e.g. G-12
    public string Category { get; set; } = string.Empty;   // Fashion, F&B, Supermarket, Electronics
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper compatibility properties
    public string Id { get => TenantId; set => TenantId = value; }
    public string Name { get => NameAr; set => NameAr = value; }
    public string FloorNumber { get; set; } = "الطابق الأول";
    public decimal CommissionRate { get; set; } = 5.00m;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public StoreLoyaltyType LoyaltyType { get; set; } = StoreLoyaltyType.MallPoints;

    // Navigation properties
    public TenantLoyaltyRule? LoyaltyRule { get; set; }
    public StoreInvoiceProfile? InvoiceProfile { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<SystemUser> SystemUsers { get; set; } = new List<SystemUser>();
    public ICollection<CustomerWallet> CustomerWallets { get; set; } = new List<CustomerWallet>();
    public ICollection<TenantSettlement> Settlements { get; set; } = new List<TenantSettlement>();
    public ICollection<StoreAlias> Aliases { get; set; } = new List<StoreAlias>();
    public ICollection<ProductBoosterRule> ProductBoosters { get; set; } = new List<ProductBoosterRule>();
}

using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class CustomerWallet
{
    public long WalletId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? TenantId { get; set; } // NULL = Unified Mall Wallet
    public BalanceType BalanceType { get; set; } = BalanceType.POINTS;
    public decimal CurrentBalance { get; set; } = 0.00m;
    public byte[]? RowVersion { get; set; } // Optimistic Concurrency Token

    // Helper properties for legacy code compatibility
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int PointsBalance
    {
        get => (int)CurrentBalance;
        set => CurrentBalance = value;
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation
    public Customer Customer { get; set; } = null!;
    public Tenant? Tenant { get; set; }
    public ICollection<WalletLedger> LedgerEntries { get; set; } = new List<WalletLedger>();
}

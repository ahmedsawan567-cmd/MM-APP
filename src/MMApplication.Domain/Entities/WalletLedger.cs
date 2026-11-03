using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class WalletLedger
{
    public long EntryId { get; set; }
    public long WalletId { get; set; }
    public Guid? InvoiceId { get; set; }
    public WalletTransactionType TransactionType { get; set; }
    public decimal AmountChanged { get; set; } // + for earning, - for redeeming
    public decimal BalanceAfter { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ReferenceRemarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper properties for legacy code compatibility
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid Id { get; set; } = Guid.NewGuid();
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string CustomerId { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int Amount { get => (int)AmountChanged; set => AmountChanged = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Description { get => ReferenceRemarks; set => ReferenceRemarks = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public PointTransactionType Type { get; set; } = PointTransactionType.BONUS;

    // Navigation
    public CustomerWallet Wallet { get; set; } = null!;
    public Invoice? Invoice { get; set; }
}

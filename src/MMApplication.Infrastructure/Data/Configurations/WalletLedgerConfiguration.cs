using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data.Configurations;

public class WalletLedgerConfiguration : IEntityTypeConfiguration<WalletLedger>
{
    public void Configure(EntityTypeBuilder<WalletLedger> builder)
    {
        builder.ToTable("WalletLedger");

        builder.HasKey(l => l.EntryId);
        builder.Property(l => l.EntryId).ValueGeneratedOnAdd();

        builder.Property(l => l.WalletId).IsRequired();
        builder.Property(l => l.InvoiceId);

        builder.Property(l => l.TransactionType)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => (WalletTransactionType)Enum.Parse(typeof(WalletTransactionType), v))
            .HasMaxLength(30);

        builder.Property(l => l.AmountChanged).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.BalanceAfter).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.ExpiresAt);
        builder.Property(l => l.ReferenceRemarks).HasMaxLength(250);
        builder.Property(l => l.CreatedAt).IsRequired();

        // Ignore helper properties
        builder.Ignore(l => l.Id);
        builder.Ignore(l => l.CustomerId);
        builder.Ignore(l => l.Type);
        builder.Ignore(l => l.Amount);
        builder.Ignore(l => l.Description);

        builder.HasOne(l => l.Wallet)
            .WithMany(w => w.LedgerEntries)
            .HasForeignKey(l => l.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Invoice)
            .WithMany(i => i.LedgerEntries)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data.Configurations;

public class CustomerWalletConfiguration : IEntityTypeConfiguration<CustomerWallet>
{
    public void Configure(EntityTypeBuilder<CustomerWallet> builder)
    {
        builder.ToTable("CustomerWallets");

        builder.HasKey(w => w.WalletId);
        builder.Property(w => w.WalletId).ValueGeneratedOnAdd();

        builder.Property(w => w.CustomerId).IsRequired().HasMaxLength(50);
        builder.Property(w => w.TenantId).HasMaxLength(50);

        builder.Property(w => w.BalanceType)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => (BalanceType)Enum.Parse(typeof(BalanceType), v))
            .HasMaxLength(20);

        builder.Property(w => w.CurrentBalance).HasPrecision(18, 2).HasDefaultValue(0.00m);

        // Optimistic Concurrency RowVersion
        builder.Property(w => w.RowVersion)
            .IsRowVersion();

        // Ignore helper properties
        builder.Ignore(w => w.PointsBalance);
        builder.Ignore(w => w.LastUpdated);

        // Unique constraint: UQ_Customer_Tenant_Wallet
        builder.HasIndex(w => new { w.CustomerId, w.TenantId, w.BalanceType })
            .IsUnique()
            .HasDatabaseName("UQ_Customer_Tenant_Wallet");

        builder.HasOne(w => w.Customer)
            .WithMany(c => c.Wallets)
            .HasForeignKey(w => w.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Tenant)
            .WithMany(t => t.CustomerWallets)
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(w => w.LedgerEntries)
            .WithOne(l => l.Wallet)
            .HasForeignKey(l => l.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

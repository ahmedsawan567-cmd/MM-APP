using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.CustomerId);
        builder.Property(c => c.CustomerId).HasMaxLength(50);

        builder.Property(c => c.FullName).IsRequired().HasMaxLength(150);
        builder.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(c => c.PhoneNumber).IsUnique();

        builder.Property(c => c.DeviceFingerprint).HasMaxLength(200);
        builder.Property(c => c.LoyaltyTier).HasMaxLength(20).HasDefaultValue("BRONZE");
        builder.Property(c => c.RegisteredAt).IsRequired();

        // Ignore compatibility helper properties
        builder.Ignore(c => c.Id);
        builder.Ignore(c => c.Tier);
        builder.Ignore(c => c.IsSuspended);
        builder.Ignore(c => c.CreatedAt);
        builder.Ignore(c => c.Role);
        builder.Ignore(c => c.StaffRole);
        builder.Ignore(c => c.PasswordHash);
        builder.Ignore(c => c.StoreTenantId);
        builder.Ignore(c => c.StoreTenant);
        builder.Ignore(c => c.Wallet);

        builder.HasMany(c => c.Wallets)
            .WithOne(w => w.Customer)
            .HasForeignKey(w => w.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Invoices)
            .WithOne(i => i.Customer)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Redemptions)
            .WithOne(r => r.Customer)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

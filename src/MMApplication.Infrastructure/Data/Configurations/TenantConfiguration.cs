using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.TenantId);
        builder.Property(t => t.TenantId).HasMaxLength(50);

        builder.Property(t => t.NameAr).IsRequired().HasMaxLength(150);
        builder.Property(t => t.NameEn).IsRequired().HasMaxLength(150);
        builder.Property(t => t.UnitNumber).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Category).IsRequired().HasMaxLength(50);
        builder.Property(t => t.LogoUrl).HasMaxLength(500);
        builder.Property(t => t.IsActive).HasDefaultValue(true);
        builder.Property(t => t.CreatedAt).IsRequired();

        // Ignore compatibility helper properties
        builder.Ignore(t => t.Id);
        builder.Ignore(t => t.Name);
        builder.Ignore(t => t.LoyaltyType); // نوع المكافأة يُدار من قاعدة الولاء للمتجر

        // بيانات تشغيلية للمتجر (تُعدَّل من صفحة المتاجر)
        builder.Property(t => t.FloorNumber).HasMaxLength(50).HasDefaultValue("");
        builder.Property(t => t.CommissionRate).HasPrecision(5, 2).HasDefaultValue(5.00m);
        builder.Property(t => t.ContactPerson).HasMaxLength(150);
        builder.Property(t => t.ContactPhone).HasMaxLength(30);

        builder.HasOne(t => t.LoyaltyRule)
            .WithOne(r => r.Tenant)
            .HasForeignKey<TenantLoyaltyRule>(r => r.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Invoices)
            .WithOne(i => i.Tenant)
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.SystemUsers)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.CustomerWallets)
            .WithOne(w => w.Tenant)
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Settlements)
            .WithOne(s => s.Tenant)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class ProductBoosterRuleConfiguration : IEntityTypeConfiguration<ProductBoosterRule>
{
    public void Configure(EntityTypeBuilder<ProductBoosterRule> builder)
    {
        builder.ToTable("ProductBoosterRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();

        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.BrandOrItemKeyword).IsRequired().HasMaxLength(200);
        builder.Property(r => r.TargetCategory).HasMaxLength(50);
        builder.Property(r => r.TenantId).HasMaxLength(50);
        builder.Property(r => r.BonusPoints).HasDefaultValue(20);
        builder.Property(r => r.PointsMultiplier).HasPrecision(5, 2).HasDefaultValue(1.00m);
        builder.Property(r => r.SponsorName).HasMaxLength(200);
        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasOne(r => r.Tenant)
            .WithMany(t => t.ProductBoosters)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.BrandOrItemKeyword);
        builder.HasIndex(r => r.TenantId);
    }
}

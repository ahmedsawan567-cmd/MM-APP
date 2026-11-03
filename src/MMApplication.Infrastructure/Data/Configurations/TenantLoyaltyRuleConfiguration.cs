using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class TenantLoyaltyRuleConfiguration : IEntityTypeConfiguration<TenantLoyaltyRule>
{
    public void Configure(EntityTypeBuilder<TenantLoyaltyRule> builder)
    {
        builder.ToTable("TenantLoyaltyRules");

        builder.HasKey(r => r.RuleId);
        builder.Property(r => r.RuleId).ValueGeneratedOnAdd();

        builder.Property(r => r.TenantId).IsRequired().HasMaxLength(50);
        builder.HasIndex(r => r.TenantId).IsUnique().HasDatabaseName("UQ_Tenant_Rule");

        builder.Property(r => r.RewardType).IsRequired().HasMaxLength(20);
        builder.Property(r => r.ConversionRate).HasPrecision(18, 4).HasDefaultValue(0.1000m);
        builder.Property(r => r.MinInvoiceTotal).HasPrecision(18, 2).HasDefaultValue(0.00m);
        builder.Property(r => r.MaxPointsCap).HasDefaultValue(500);
        builder.Property(r => r.PointsExpiryDays).HasDefaultValue(180);
        builder.Property(r => r.MaxInvoiceAgeDays).HasDefaultValue(7);
        builder.Property(r => r.DynamicRulesJson);
        builder.Property(r => r.UpdatedAt).IsRequired();
    }
}

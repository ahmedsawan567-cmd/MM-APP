using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class LoyaltyEarningRuleConfiguration : IEntityTypeConfiguration<LoyaltyEarningRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyEarningRule> builder)
    {
        builder.ToTable("LoyaltyEarningRules");
        builder.HasKey(r => r.RuleId);
        builder.Property(r => r.RuleId).ValueGeneratedOnAdd();

        builder.Property(r => r.StoreTenantId).HasMaxLength(50);
        builder.Property(r => r.RuleName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.CalculationType).IsRequired().HasMaxLength(50).HasDefaultValue("SPEND_RATIO");
        builder.Property(r => r.FixedPointsPerInvoice).HasDefaultValue(0);
        builder.Property(r => r.SpendRatioRate).HasPrecision(5, 4).HasDefaultValue(0.1000m);
        builder.Property(r => r.MinInvoiceAmount).HasPrecision(18, 2).HasDefaultValue(0.00m);
        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasOne(r => r.StoreTenant)
            .WithMany()
            .HasForeignKey(r => r.StoreTenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.StoreTenantId);
        builder.HasIndex(r => r.CalculationType);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class MembershipTierConfiguration : IEntityTypeConfiguration<MembershipTier>
{
    public void Configure(EntityTypeBuilder<MembershipTier> builder)
    {
        builder.ToTable("MembershipTiers");
        builder.HasKey(t => t.Code);
        builder.Property(t => t.Code).HasMaxLength(20);
        builder.Property(t => t.NameAr).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Benefits).HasMaxLength(1000);
        builder.Property(t => t.Color).HasMaxLength(9);
        builder.Property(t => t.MinSpend).HasPrecision(18, 2);
        builder.Property(t => t.PointsMultiplier).HasPrecision(5, 2);
    }
}

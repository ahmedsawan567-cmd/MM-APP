using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data.Configurations;

public class RewardRedemptionConfiguration : IEntityTypeConfiguration<RewardRedemption>
{
    public void Configure(EntityTypeBuilder<RewardRedemption> builder)
    {
        builder.ToTable("RewardRedemptions");

        builder.HasKey(rr => rr.Id);

        builder.Property(rr => rr.CustomerId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(rr => rr.RewardId)
            .IsRequired();

        builder.Property(rr => rr.RedemptionCode)
            .IsRequired()
            .HasMaxLength(64);

        // Unique index on RedemptionCode
        builder.HasIndex(rr => rr.RedemptionCode)
            .IsUnique();

        builder.Property(rr => rr.PointsSpent)
            .IsRequired();

        builder.Property(rr => rr.Status)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => (RedemptionStatus)Enum.Parse(typeof(RedemptionStatus), v)
            )
            .HasMaxLength(50);

        builder.Property(rr => rr.UsedByMerchant)
            .HasMaxLength(200);

        builder.Property(rr => rr.RedeemedAt)
            .IsRequired();

        builder.Property(rr => rr.ExpiresAt)
            .IsRequired();

        // Foreign keys
        builder.HasOne(rr => rr.Customer)
            .WithMany(c => c.Redemptions)
            .HasForeignKey(rr => rr.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(rr => rr.Reward)
            .WithMany(r => r.Redemptions)
            .HasForeignKey(rr => rr.RewardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rr => rr.CustomerId);
    }
}

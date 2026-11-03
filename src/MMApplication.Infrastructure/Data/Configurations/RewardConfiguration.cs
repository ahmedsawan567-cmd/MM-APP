using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class RewardConfiguration : IEntityTypeConfiguration<Reward>
{
    public void Configure(EntityTypeBuilder<Reward> builder)
    {
        builder.ToTable("Rewards");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.MerchantName)
            .HasMaxLength(200);

        builder.Property(r => r.PointsCost)
            .IsRequired();

        builder.Property(r => r.DiscountValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(r => r.ImageUrl)
            .HasMaxLength(500);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.ValidityDays)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(r => r.CreatedAt)
            .IsRequired();
    }
}

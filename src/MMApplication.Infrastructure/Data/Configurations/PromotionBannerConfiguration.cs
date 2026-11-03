using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class PromotionBannerConfiguration : IEntityTypeConfiguration<PromotionBanner>
{
    public void Configure(EntityTypeBuilder<PromotionBanner> builder)
    {
        builder.ToTable("PromotionBanners");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Subtitle)
            .HasMaxLength(300);

        builder.Property(b => b.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.TargetUrl)
            .HasMaxLength(500);

        builder.Property(b => b.TargetAudience)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.DisplayOrder)
            .IsRequired();

        builder.Property(b => b.IsActive)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();
    }
}

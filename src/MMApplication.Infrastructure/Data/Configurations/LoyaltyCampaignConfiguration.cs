using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class LoyaltyCampaignConfiguration : IEntityTypeConfiguration<LoyaltyCampaign>
{
    public void Configure(EntityTypeBuilder<LoyaltyCampaign> builder)
    {
        builder.ToTable("LoyaltyCampaigns");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.PointsMultiplier)
            .IsRequired()
            .HasColumnType("decimal(4,2)");

        builder.Property(c => c.MinSpendAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.StartDate)
            .IsRequired();

        builder.Property(c => c.EndDate)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class FraudReviewLogConfiguration : IEntityTypeConfiguration<FraudReviewLog>
{
    public void Configure(EntityTypeBuilder<FraudReviewLog> builder)
    {
        builder.ToTable("FraudReviewLogs");

        builder.HasKey(r => r.ReviewId);
        builder.Property(r => r.ReviewId).ValueGeneratedOnAdd();

        builder.Property(r => r.InvoiceId).IsRequired();
        builder.Property(r => r.AssignedAuditorId).HasMaxLength(50);
        builder.Property(r => r.CalculatedRiskScore).IsRequired();
        builder.Property(r => r.TriggeredFlags).IsRequired();
        builder.Property(r => r.AuditorDecision).HasMaxLength(20);
        builder.Property(r => r.DecisionReason).HasMaxLength(250);
        builder.Property(r => r.ResolvedAt);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.Invoice)
            .WithMany(i => i.FraudReviews)
            .HasForeignKey(r => r.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.AssignedAuditor)
            .WithMany(u => u.AssignedReviews)
            .HasForeignKey(r => r.AssignedAuditorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

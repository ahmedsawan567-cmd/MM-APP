using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data.Configurations;

public class TenantSettlementConfiguration : IEntityTypeConfiguration<TenantSettlement>
{
    public void Configure(EntityTypeBuilder<TenantSettlement> builder)
    {
        builder.ToTable("TenantSettlements");

        builder.HasKey(s => s.SettlementId);
        builder.Property(s => s.SettlementId).ValueGeneratedOnAdd();

        builder.Property(s => s.TenantId).IsRequired().HasMaxLength(50);
        builder.Property(s => s.PeriodStartDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.PeriodEndDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.TotalVouchersRedeemedValue).HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.MallServiceFee).HasPrecision(18, 2).HasDefaultValue(0.00m);
        builder.Property(s => s.NetPayableToTenant).HasPrecision(18, 2).IsRequired();

        builder.Property(s => s.SettlementStatus)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => (SettlementStatus)Enum.Parse(typeof(SettlementStatus), v))
            .HasMaxLength(30)
            .HasDefaultValue(SettlementStatus.OPEN);

        builder.Property(s => s.SettledByUserId).HasMaxLength(50);
        builder.Property(s => s.SettledAt);

        // Ignore compatibility helpers
        builder.Ignore(s => s.Id);
        builder.Ignore(s => s.StoreTenantId);
        builder.Ignore(s => s.StoreTenant);
        builder.Ignore(s => s.PeriodLabel);
        builder.Ignore(s => s.RedemptionsCount);
        builder.Ignore(s => s.TotalRedemptionsValue);
        builder.Ignore(s => s.MallCommissionAmount);
        builder.Ignore(s => s.NetPayableAmount);
        builder.Ignore(s => s.Status);
        builder.Ignore(s => s.PeriodStart);
        builder.Ignore(s => s.PeriodEnd);
        builder.Ignore(s => s.CreatedAt);

        builder.HasOne(s => s.Tenant)
            .WithMany(t => t.Settlements)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SettledByUser)
            .WithMany(u => u.SettledSettlements)
            .HasForeignKey(s => s.SettledByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

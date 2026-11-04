using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class StoreInvoiceProfileConfiguration : IEntityTypeConfiguration<StoreInvoiceProfile>
{
    public void Configure(EntityTypeBuilder<StoreInvoiceProfile> builder)
    {
        builder.ToTable("StoreInvoiceProfiles");
        builder.HasKey(p => p.TenantId);
        builder.Property(p => p.TenantId).HasMaxLength(50);
        builder.Property(p => p.SampleImageUrl).HasMaxLength(500);
        builder.Property(p => p.PrintedName).HasMaxLength(200);
        builder.Property(p => p.TaxNumber).HasMaxLength(60);
        builder.Property(p => p.PhoneOnReceipt).HasMaxLength(60);
        builder.Property(p => p.InvoiceNumberLabel).HasMaxLength(100);
        builder.Property(p => p.InvoiceNumberExample).HasMaxLength(100);
        builder.Property(p => p.InvoiceNumberPattern).HasMaxLength(200);
        builder.Property(p => p.TotalLabel).HasMaxLength(100);
        builder.Property(p => p.DateFormat).HasMaxLength(40);
        builder.Property(p => p.Currency).HasMaxLength(10);
        builder.Property(p => p.LayoutNotes).HasMaxLength(1000);
        builder.Property(p => p.UpdatedBy).HasMaxLength(150);

        builder.HasOne(p => p.Tenant)
            .WithOne(t => t.InvoiceProfile)
            .HasForeignKey<StoreInvoiceProfile>(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

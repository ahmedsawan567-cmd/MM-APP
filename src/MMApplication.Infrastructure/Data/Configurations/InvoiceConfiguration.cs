using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.InvoiceId);
        builder.Property(i => i.InvoiceId).ValueGeneratedOnAdd();

        builder.Property(i => i.CustomerId).IsRequired().HasMaxLength(50);
        builder.Property(i => i.TenantId).HasMaxLength(50);

        builder.Property(i => i.ExtractedMerchant).HasMaxLength(150);
        builder.Property(i => i.InvoiceNumber).HasMaxLength(100);
        builder.Property(i => i.InvoiceDate);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.ConfidenceScore).HasPrecision(5, 4);
        builder.Property(i => i.RawAiPayload);

        builder.Property(i => i.ReceiptImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(i => i.ImagePHash).HasMaxLength(64);
        builder.Property(i => i.FraudRiskScore).HasDefaultValue(0);

        builder.Property(i => i.Source).HasMaxLength(10).HasDefaultValue("PHOTO");

        builder.Property(i => i.ProcessingStatus)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => (InvoiceStatus)Enum.Parse(typeof(InvoiceStatus), v))
            .HasMaxLength(30)
            .HasDefaultValue(InvoiceStatus.PENDING);

        builder.Property(i => i.RejectionReason).HasMaxLength(250);
        builder.Property(i => i.PointsAwarded).HasDefaultValue(0);
        builder.Property(i => i.CashbackAwarded).HasPrecision(18, 2).HasDefaultValue(0.00m);
        builder.Property(i => i.CreatedAt).IsRequired();

        // Ignore compatibility helper properties
        builder.Ignore(i => i.Id);
        builder.Ignore(i => i.MerchantName);
        builder.Ignore(i => i.ImageUrl);
        builder.Ignore(i => i.RiskScore);
        builder.Ignore(i => i.PhotoHash);
        builder.Ignore(i => i.Status);
        builder.Ignore(i => i.PointsEarned);
        builder.Ignore(i => i.FraudFlagReason);

        // مكافحة الاحتيال: رقم الفاتورة (بصيغته الموحّدة) لا يتكرر لنفس المتجر أبداً
        // (PostgreSQL يسمح بتكرار القيم الفارغة في الفهرس الفريد: الفواتير غير المقروءة بعد)
        builder.Property(i => i.InvoiceNumberKey).HasMaxLength(100);
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumberKey })
            .IsUnique()
            .HasDatabaseName("UQ_Invoices_Tenant_InvoiceKey");
        builder.HasIndex(i => i.InvoiceNumberKey);

        // Indexes
        builder.HasIndex(i => new { i.CustomerId, i.CreatedAt })
            .HasDatabaseName("IX_Invoices_Customer_Date");

        builder.HasIndex(i => i.ImagePHash)
            .HasDatabaseName("IX_Invoices_PHash");

        builder.HasOne(i => i.Customer)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Tenant)
            .WithMany(t => t.Invoices)
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(i => i.LineItems)
            .WithOne(li => li.Invoice)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.LedgerEntries)
            .WithOne(l => l.Invoice)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(i => i.FraudReviews)
            .WithOne(fr => fr.Invoice)
            .HasForeignKey(fr => fr.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

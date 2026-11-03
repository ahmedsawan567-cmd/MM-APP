using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(li => li.LineItemId);
        builder.Property(li => li.LineItemId).ValueGeneratedOnAdd();

        builder.Property(li => li.InvoiceId).IsRequired();
        builder.Property(li => li.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(li => li.Quantity).HasPrecision(10, 2).HasDefaultValue(1.0m);
        builder.Property(li => li.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(li => li.TotalPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(li => li.Confidence).HasPrecision(5, 4);

        builder.HasOne(li => li.Invoice)
            .WithMany(i => i.LineItems)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

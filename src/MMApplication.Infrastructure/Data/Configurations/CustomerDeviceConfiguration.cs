using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class CustomerDeviceConfiguration : IEntityTypeConfiguration<CustomerDevice>
{
    public void Configure(EntityTypeBuilder<CustomerDevice> builder)
    {
        builder.ToTable("CustomerDevices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();

        builder.Property(d => d.CustomerId).IsRequired().HasMaxLength(50);
        builder.Property(d => d.DeviceFingerprint).IsRequired().HasMaxLength(150);
        builder.Property(d => d.DeviceModel).HasMaxLength(100);
        builder.Property(d => d.OsVersion).HasMaxLength(50);
        builder.Property(d => d.IpAddress).HasMaxLength(50);

        builder.HasOne(d => d.Customer)
            .WithMany(c => c.Devices)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.DeviceFingerprint);
        builder.HasIndex(d => d.CustomerId);
    }
}

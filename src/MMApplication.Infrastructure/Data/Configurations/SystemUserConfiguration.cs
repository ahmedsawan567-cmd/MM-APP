using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data.Configurations;

public class SystemUserConfiguration : IEntityTypeConfiguration<SystemUser>
{
    public void Configure(EntityTypeBuilder<SystemUser> builder)
    {
        builder.ToTable("SystemUsers");

        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasMaxLength(50);

        builder.Property(u => u.TenantId).HasMaxLength(50);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(150);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => (SystemUserRole)Enum.Parse(typeof(SystemUserRole), v))
            .HasMaxLength(30);

        builder.Property(u => u.PinCodeHash).HasMaxLength(200);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.SystemUsers)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.SettledSettlements)
            .WithOne(s => s.SettledByUser)
            .HasForeignKey(s => s.SettledByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.AssignedReviews)
            .WithOne(r => r.AssignedAuditor)
            .HasForeignKey(r => r.AssignedAuditorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

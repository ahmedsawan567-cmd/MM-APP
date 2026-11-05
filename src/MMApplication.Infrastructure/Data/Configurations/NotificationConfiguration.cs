using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.RecipientType).IsRequired().HasMaxLength(10);
        builder.Property(n => n.RecipientId).IsRequired().HasMaxLength(50);
        builder.Property(n => n.Type).IsRequired().HasMaxLength(40);
        builder.Property(n => n.Title).IsRequired().HasMaxLength(150);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(600);
        builder.Property(n => n.Link).HasMaxLength(300);
        builder.HasIndex(n => new { n.RecipientType, n.RecipientId, n.IsRead, n.CreatedAt }).HasDatabaseName("IX_Notifications_Recipient");
    }
}

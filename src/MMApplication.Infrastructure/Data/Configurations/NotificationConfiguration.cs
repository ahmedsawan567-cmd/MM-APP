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
        builder.HasIndex(n => n.BroadcastId).HasDatabaseName("IX_Notifications_Broadcast");
        builder.HasIndex(n => new { n.PushSentAt, n.CreatedAt }).HasDatabaseName("IX_Notifications_Push");
    }
}

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.CustomerId).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Token).IsRequired().HasMaxLength(512);
        builder.Property(d => d.Platform).IsRequired().HasMaxLength(20);
        builder.HasIndex(d => d.Token).IsUnique().HasDatabaseName("UQ_DeviceTokens_Token");
        builder.HasIndex(d => d.CustomerId).HasDatabaseName("IX_DeviceTokens_Customer");
    }
}

public class NotificationBroadcastConfiguration : IEntityTypeConfiguration<NotificationBroadcast>
{
    public void Configure(EntityTypeBuilder<NotificationBroadcast> builder)
    {
        builder.ToTable("NotificationBroadcasts");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Title).IsRequired().HasMaxLength(150);
        builder.Property(b => b.Body).IsRequired().HasMaxLength(600);
        builder.Property(b => b.Audience).IsRequired().HasMaxLength(20);
        builder.Property(b => b.SentBy).IsRequired().HasMaxLength(100);
        builder.HasIndex(b => b.SentAt).HasDatabaseName("IX_NotificationBroadcasts_SentAt");
    }
}

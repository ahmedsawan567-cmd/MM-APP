namespace MMApplication.Domain.Entities;

/// <summary>رمز جهاز العميل لإشعارات Firebase (FCM). العميل قد يملك أكثر من جهاز.</summary>
public class DeviceToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

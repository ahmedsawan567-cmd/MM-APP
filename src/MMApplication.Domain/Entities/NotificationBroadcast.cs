namespace MMApplication.Domain.Entities;

/// <summary>سجل إشعار جماعي أرسلته الإدارة (صف واحد لكل إرسال، والمستلمون في جدول Notifications).</summary>
public class NotificationBroadcast
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Audience { get; set; } = "ALL";
    public int RecipientsCount { get; set; }
    public string SentBy { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    /// <summary>عند السحب تُحذف نسخ الإشعار من تطبيقات العملاء ويبقى السجل.</summary>
    public DateTime? RetractedAt { get; set; }
}

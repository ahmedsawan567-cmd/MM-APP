namespace MMApplication.Domain.Entities;

/// <summary>
/// إشعار لمستلم واحد: عميل (يظهر في التطبيق) أو موظف (يظهر في جرس المنصة).
/// كل مستلم له صفّه الخاص فتُتابع حالة القراءة لكل شخص.
/// </summary>
public class Notification
{
    public const string ToCustomer = "CUSTOMER";
    public const string ToStaff = "STAFF";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string RecipientType { get; set; } = ToCustomer;
    /// <summary>CustomerId للعميل أو UserId للموظف.</summary>
    public string RecipientId { get; set; } = string.Empty;
    /// <summary>نوع الحدث (INVOICE_APPROVED، REVIEW_NEEDED، BROADCAST...).</summary>
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>للموظف: رابط صفحة في المنصة. للعميل: وجهة داخل التطبيق (مثل invoice:{id}).</summary>
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

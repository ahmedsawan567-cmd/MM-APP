using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data;

/// <summary>
/// إنشاء الإشعارات. الدوال تضيف الصفوف إلى السياق فقط، وتُحفظ مع عملية المستدعي
/// (فلا يصل إشعار عن عملية لم تكتمل).
/// </summary>
public static class Notifier
{
    public const int RetentionDays = 90;

    public static void Customer(AppDbContext db, string customerId, string type, string title, string body, string? link = null) =>
        db.Notifications.Add(new Notification
        {
            RecipientType = Notification.ToCustomer,
            RecipientId = customerId,
            Type = type,
            Title = Trim(title, 150),
            Body = Trim(body, 600),
            Link = link,
        });

    /// <summary>
    /// إشعار للموظفين المعنيين: المدير العام ومدقق الفواتير دائماً، ومدير المتجر وكاشيره إن كان الحدث يخص متجرهم.
    /// </summary>
    public static async Task StaffAsync(AppDbContext db, string type, string title, string body, string? link = null, string? tenantId = null)
    {
        var recipients = await db.SystemUsers.AsNoTracking()
            .Where(u => u.IsActive && (u.Role == SystemUserRole.SUPER_ADMIN || u.Role == SystemUserRole.FRAUD_AUDITOR ||
                                       (tenantId != null && u.TenantId == tenantId && u.Role == SystemUserRole.STORE_MANAGER)))
            .Select(u => u.UserId)
            .ToListAsync();
        foreach (var id in recipients)
        {
            db.Notifications.Add(new Notification
            {
                RecipientType = Notification.ToStaff,
                RecipientId = id,
                Type = type,
                Title = Trim(title, 150),
                Body = Trim(body, 600),
                Link = link,
            });
        }
    }

    /// <summary>إعلان من الإدارة لشريحة من العملاء. يرجع عدد المستلمين.</summary>
    public static async Task<int> BroadcastAsync(AppDbContext db, string segment, string title, string body)
    {
        var query = db.Customers.AsNoTracking().Where(c => !c.IsArchived && !c.IsFrozen);
        query = segment switch
        {
            "VIP_GOLD" => query.Where(c => c.LoyaltyTier == "GOLD" || c.LoyaltyTier == "VIP"),
            "SILVER" => query.Where(c => c.LoyaltyTier == "SILVER"),
            "BRONZE" => query.Where(c => c.LoyaltyTier == "BRONZE"),
            _ => query,
        };
        var ids = await query.Select(c => c.CustomerId).ToListAsync();
        var now = DateTime.UtcNow;
        db.Notifications.AddRange(ids.Select(id => new Notification
        {
            RecipientType = Notification.ToCustomer,
            RecipientId = id,
            Type = "BROADCAST",
            Title = Trim(title, 150),
            Body = Trim(body, 600),
            CreatedAt = now,
        }));
        return ids.Count;
    }

    /// <summary>حذف الإشعارات الأقدم من مدة الاحتفاظ (عند تشغيل الخادم).</summary>
    public static async Task<int> PurgeOldAsync(AppDbContext db)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        var old = await db.Notifications.Where(n => n.CreatedAt < cutoff).ToListAsync();
        if (old.Count == 0) return 0;
        db.Notifications.RemoveRange(old);
        await db.SaveChangesAsync();
        return old.Count;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data;

/// <summary>
/// حماية حسابات طاقم العمل من تخمين كلمة المرور: قفل مؤقت بعد محاولات فاشلة متتالية.
/// التغييرات تُضاف للسياق ويحفظها المستدعي.
/// </summary>
public static class StaffLoginGuard
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>هل الحساب مقفل الآن؟ ويرجع الدقائق المتبقية.</summary>
    public static bool IsLocked(SystemUser user, out int minutesLeft)
    {
        minutesLeft = 0;
        if (user.LockedUntil is not { } until || until <= DateTime.UtcNow) return false;
        minutesLeft = Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalMinutes));
        return true;
    }

    public static string LockedMessage(int minutes) =>
        $"الحساب مقفل مؤقتاً بسبب محاولات دخول خاطئة متكررة. حاول بعد {minutes} دقيقة، أو اطلب من المدير العام كلمة مرور جديدة.";

    /// <summary>تسجيل محاولة فاشلة. يرجع true إذا أدت هذه المحاولة لقفل الحساب.</summary>
    public static bool RegisterFailure(AppDbContext db, SystemUser user, string? ip = null)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount < MaxFailedAttempts) return false;

        user.FailedLoginCount = 0;
        user.LockedUntil = DateTime.UtcNow + LockDuration;
        db.AuditLogs.Add(new AuditLog
        {
            PerformedBy = "النظام",
            Action = "STAFF_LOCKED",
            EntityName = "SystemUser",
            EntityId = user.UserId,
            Details = $"قفل حساب {user.FullName} لمدة {LockDuration.TotalMinutes:0} دقيقة بعد {MaxFailedAttempts} محاولات دخول خاطئة",
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow,
        });
        return true;
    }

    public static void RegisterSuccess(SystemUser user)
    {
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
    }

    /// <summary>فك القفل (عند ضبط كلمة مرور جديدة من الإدارة).</summary>
    public static void Unlock(SystemUser user)
    {
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
    }
}

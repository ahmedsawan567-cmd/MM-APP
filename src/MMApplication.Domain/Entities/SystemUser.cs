using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class SystemUser
{
    public string UserId { get; set; } = string.Empty;
    public string? TenantId { get; set; } // NULL = Mall Admin
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public SystemUserRole Role { get; set; } = SystemUserRole.SUPER_ADMIN;
    public string? PinCodeHash { get; set; } // 4-digit quick cashier pin
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>آخر دخول ناجح للمنصة أو التطبيق.</summary>
    public DateTime? LastLoginAt { get; set; }
    /// <summary>محاولات الدخول الفاشلة المتتالية (تُصفّر عند النجاح أو القفل).</summary>
    public int FailedLoginCount { get; set; }
    /// <summary>الحساب مقفل مؤقتاً حتى هذا الوقت بعد محاولات فاشلة متكررة.</summary>
    public DateTime? LockedUntil { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
    public ICollection<TenantSettlement> SettledSettlements { get; set; } = new List<TenantSettlement>();
    public ICollection<FraudReviewLog> AssignedReviews { get; set; } = new List<FraudReviewLog>();
}

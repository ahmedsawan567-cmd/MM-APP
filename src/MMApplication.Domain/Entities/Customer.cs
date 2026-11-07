using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class Customer
{
    public string CustomerId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>رقم العضوية المميز الظاهر للعميل وللموظفين (مثل MM-100001). فريد ولا يتغير.</summary>
    public string? MembershipNumber { get; set; }
    public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    /// <summary>M أو F (اختياري).</summary>
    public string? Gender { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string LoyaltyTier { get; set; } = "BRONZE"; // رمز مستوى العضوية من جدول MembershipTiers
    /// <summary>العضوية مثبّتة يدوياً من موظف: لا تتغير تلقائياً.</summary>
    public bool TierLocked { get; set; }
    public DateTime? TierUpdatedAt { get; set; }
    public bool IsFrozen { get; set; } = false;
    public bool IsArchived { get; set; } = false;        // Soft archiving / deactivation without deleting ledger
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // ---- نشاط الحساب (للعرض في ملف العميل) ----
    /// <summary>آخر دخول بكلمة المرور في التطبيق.</summary>
    public DateTime? LastLoginAt { get; set; }
    /// <summary>عدد مرات الدخول بكلمة المرور.</summary>
    public int LoginCount { get; set; }
    /// <summary>آخر مرة فتح فيها التطبيق (أي طلب من التطبيق وهو داخل حسابه).</summary>
    public DateTime? LastSeenAt { get; set; }
    /// <summary>إصدار التطبيق ونوع الجهاز في آخر استخدام.</summary>
    public string? LastAppVersion { get; set; }
    public string? LastPlatform { get; set; }
    /// <summary>آخر تغيير لكلمة المرور ومن غيّرها (العميل أو اسم الموظف). كلمة المرور نفسها لا تُحفظ إلا مشفّرة.</summary>
    public DateTime? PasswordChangedAt { get; set; }
    public string? PasswordChangedBy { get; set; }

    // Backward compatibility helper properties for legacy view bindings
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Id { get => CustomerId; set => CustomerId = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Tier { get => LoyaltyTier; set => LoyaltyTier = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsSuspended { get => IsFrozen; set => IsFrozen = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime CreatedAt { get => RegisteredAt; set => RegisteredAt = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public CustomerRole Role { get; set; } = CustomerRole.Customer;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public StaffRole? StaffRole { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PasswordHash { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid? StoreTenantId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Tenant? StoreTenant { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public CustomerWallet? Wallet
    {
        get => Wallets.FirstOrDefault(w => w.TenantId == null) ?? Wallets.FirstOrDefault();
        set
        {
            if (value != null && !Wallets.Contains(value))
            {
                Wallets.Add(value);
            }
        }
    }

    // Navigation properties
    public ICollection<CustomerWallet> Wallets { get; set; } = new List<CustomerWallet>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<RewardRedemption> Redemptions { get; set; } = new List<RewardRedemption>();
    public ICollection<CustomerDevice> Devices { get; set; } = new List<CustomerDevice>();
}

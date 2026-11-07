using System.ComponentModel.DataAnnotations;

namespace MMApplication.Application.DTOs;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "الاسم يجب أن يكون بين حرفين و 200 حرف")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الهاتف مطلوب")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [MinLength(6, ErrorMessage = "يجب ألا تقل كلمة المرور عن 6 أحرف")]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequestDto
{
    [Required(ErrorMessage = "رقم الهاتف مطلوب")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? MembershipNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public int PointsBalance { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? MembershipNumber { get; set; }
    public string? Email { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string LoyaltyTier { get; set; } = "BRONZE";
    /// <summary>اسم العضوية بالعربية كما ضبطه المول.</summary>
    public string LoyaltyTierName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int PointsBalance { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>تعديل العميل لبياناته من التطبيق. رقم الهاتف لا يُعدَّل إلا من الإدارة (هو هوية الحساب).</summary>
public class UpdateProfileRequestDto
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين 3 و 150 حرفاً")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [StringLength(150)]
    public string? Email { get; set; }

    public DateTime? BirthDate { get; set; }

    [RegularExpression("^[MF]$", ErrorMessage = "قيمة الجنس غير صالحة")]
    public string? Gender { get; set; }
}

public class ChangePasswordRequestDto
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [MinLength(8, ErrorMessage = "يجب ألا تقل كلمة المرور الجديدة عن 8 أحرف")]
    public string NewPassword { get; set; } = string.Empty;
}

public class DeleteAccountRequestDto
{
    [Required(ErrorMessage = "كلمة المرور مطلوبة لتأكيد الحذف")]
    public string Password { get; set; } = string.Empty;
}

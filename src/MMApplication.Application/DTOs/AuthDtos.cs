using System.ComponentModel.DataAnnotations;

namespace MMApplication.Application.DTOs;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "الاسم يجب أن يكون بين حرفين و 200 حرف")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الهاتف مطلوب")]
    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
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
    public string Role { get; set; } = string.Empty;
    public int PointsBalance { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int PointsBalance { get; set; }
    public DateTime CreatedAt { get; set; }
}

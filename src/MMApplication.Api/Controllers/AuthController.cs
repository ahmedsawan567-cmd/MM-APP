using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Application.Common;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IJwtTokenService _jwtService;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext context,
        IJwtTokenService jwtService,
        IPasswordHasherService passwordHasher,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// تسجيل حساب عميل جديد والحصول على رمز المصادقة (JWT Token).
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var normalizedPhone = PhoneNumbers.Normalize(request.PhoneNumber);
        if (normalizedPhone == null)
        {
            return BadRequest(new { success = false, message = PhoneNumbers.InvalidMessage });
        }

        var phoneExists = await _context.Customers.AnyAsync(c => c.PhoneNumber == normalizedPhone || c.PhoneNumber == request.PhoneNumber.Trim());
        if (phoneExists)
        {
            return Conflict(new
            {
                success = false,
                message = "رقم الهاتف مسجل مسبقاً. إذا كان رقمك، سجّل الدخول أو تواصل مع خدمة العملاء."
            });
        }

        var customerId = $"CUST-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        var customer = new Customer
        {
            CustomerId = customerId,
            FullName = request.FullName.Trim(),
            PhoneNumber = normalizedPhone,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            LoyaltyTier = "BRONZE",
            IsFrozen = false,
            RegisteredAt = DateTime.UtcNow
        };

        var wallet = new CustomerWallet
        {
            CustomerId = customerId,
            BalanceType = BalanceType.POINTS,
            CurrentBalance = 0
        };

        await _context.Customers.AddAsync(customer);
        await _context.CustomerWallets.AddAsync(wallet);
        await CustomerAccounts.SaveNewCustomerAsync(_context, customer);

        var token = _jwtService.GenerateToken(customer, out var expiresAt);

        _logger.LogInformation("تم تسجيل عميل جديد بنجاح: {CustomerId} ({Phone})", customer.CustomerId, customer.PhoneNumber);

        return Ok(new AuthResponseDto
        {
            Success = true,
            Message = "تم إنشاء الحساب وتسجيل الدخول بنجاح (Account created successfully).",
            Token = token,
            CustomerId = customer.CustomerId,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            MembershipNumber = customer.MembershipNumber,
            Role = "CUSTOMER",
            PointsBalance = 0,
            ExpiresAt = expiresAt
        });
    }

    /// <summary>
    /// تسجيل الدخول برقم الهاتف وكلمة المرور والحصول على رمز المصادقة (JWT Token).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var rawLogin = request.PhoneNumber.Trim();
        var normalizedPhone = PhoneNumbers.Normalize(rawLogin) ?? rawLogin;

        // 1. أولاً فحص إذا كان موظف/مسؤول نظام في SystemUsers
        var staffUser = await _context.SystemUsers
            .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone || u.PhoneNumber == rawLogin || u.Email == rawLogin);

        if (staffUser != null)
        {
            if (StaffLoginGuard.IsLocked(staffUser, out var minutesLeft))
                return Unauthorized(new { success = false, message = StaffLoginGuard.LockedMessage(minutesLeft) });

            var isValidStaffPassword = _passwordHasher.VerifyPassword(request.Password, staffUser.PasswordHash);
            if (!isValidStaffPassword)
            {
                StaffLoginGuard.RegisterFailure(_context, staffUser, HttpContext?.Connection.RemoteIpAddress?.ToString());
                await _context.SaveChangesAsync();
            }
            if (isValidStaffPassword && staffUser.IsActive)
            {
                StaffLoginGuard.RegisterSuccess(staffUser);
                await _context.SaveChangesAsync();
                var staffToken = _jwtService.GenerateToken(staffUser, out var staffExpiry);
                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "تم تسجيل دخول طاقم العمل بنجاح (Staff Login successful).",
                    Token = staffToken,
                    CustomerId = staffUser.UserId,
                    FullName = staffUser.FullName,
                    PhoneNumber = staffUser.PhoneNumber ?? "-",
                    Role = staffUser.Role.ToString(),
                    PointsBalance = 0,
                    ExpiresAt = staffExpiry
                });
            }
        }

        // 2. إذا لم يكن موظفاً، فحص جدول المتسوقين Customers
        var customer = await _context.Customers
            .Include(c => c.Wallets)
            .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhone || c.PhoneNumber == rawLogin);

        if (customer == null || customer.IsFrozen || customer.IsArchived)
        {
            return Unauthorized(new
            {
                success = false,
                message = "رقم الهاتف غير مسجل أو الحساب معطل (Invalid credentials or account frozen)."
            });
        }

        // حساب بدون كلمة مرور (مثلاً أُنشئ من مكتب خدمة العملاء) لا يُسمح بالدخول إليه بأي كلمة مرور،
        // وإلا أمكن لأي شخص يعرف رقم الهاتف الدخول إلى الحساب.
        if (string.IsNullOrEmpty(customer.PasswordHash))
        {
            return Unauthorized(new
            {
                success = false,
                message = "هذا الحساب غير مفعّل لتطبيق الجوال بعد. يرجى مراجعة مكتب خدمة العملاء في المول لتفعيله (Account has no password set)."
            });
        }

        if (!_passwordHasher.VerifyPassword(request.Password, customer.PasswordHash))
        {
            return Unauthorized(new
            {
                success = false,
                message = "كلمة المرور غير صحيحة (Incorrect password)."
            });
        }

        var token = _jwtService.GenerateToken(customer, out var expiresAt);

        _logger.LogInformation("تسجيل دخول ناجح للعميل: {CustomerId}", customer.CustomerId);

        return Ok(new AuthResponseDto
        {
            Success = true,
            Message = "تم تسجيل الدخول بنجاح (Logged in successfully).",
            Token = token,
            CustomerId = customer.CustomerId,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            MembershipNumber = customer.MembershipNumber,
            Role = "CUSTOMER",
            PointsBalance = (int)(customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS)?.CurrentBalance ?? 0),
            ExpiresAt = expiresAt
        });
    }

    /// <summary>
    /// استعلام الملف الشخصي ورصيد محفظة العميل الحالي المسجل دخوله عبر الـ JWT Token.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized(new { success = false, message = "غير مصرح (Unauthorized)." });
        }

        var customer = await _context.Customers
            .Include(c => c.Wallets)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == currentUserId);

        if (customer == null)
        {
            return NotFound(new { success = false, message = "حساب المستخدم غير موجود." });
        }

        return Ok(ToProfile(customer));
    }

    private static UserProfileDto ToProfile(Customer customer) => new()
    {
        Id = customer.CustomerId,
        FullName = customer.FullName,
        PhoneNumber = customer.PhoneNumber,
        MembershipNumber = customer.MembershipNumber,
        Email = customer.Email,
        BirthDate = customer.BirthDate,
        Gender = customer.Gender,
        LoyaltyTier = customer.LoyaltyTier,
        Role = "CUSTOMER",
        PointsBalance = (int)(customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS)?.CurrentBalance ?? 0),
        CreatedAt = customer.RegisteredAt
    };

    private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

    private async Task<Customer?> CurrentCustomerAsync() =>
        CurrentUserId is { Length: > 0 } id
            ? await _context.Customers.Include(c => c.Wallets).FirstOrDefaultAsync(c => c.CustomerId == id && !c.IsArchived)
            : null;

    /// <summary>تعديل العميل لبياناته الشخصية (الاسم، البريد، تاريخ الميلاد، الجنس).</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = FirstError() });
        var customer = await CurrentCustomerAsync();
        if (customer == null) return NotFound(new { success = false, message = "حساب المستخدم غير موجود." });

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (email != null && await _context.Customers.AnyAsync(c => c.Email == email && c.CustomerId != customer.CustomerId))
        {
            return Conflict(new { success = false, message = "البريد الإلكتروني مستخدم في حساب آخر." });
        }
        if (request.BirthDate is { } birth && (birth.Date > DateTime.UtcNow.Date.AddYears(-5) || birth.Year < 1900))
        {
            return BadRequest(new { success = false, message = "تاريخ الميلاد غير صالح." });
        }

        customer.FullName = request.FullName.Trim();
        customer.Email = email;
        customer.BirthDate = request.BirthDate?.Date;
        customer.Gender = request.Gender;
        customer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "تم حفظ بياناتك.", profile = ToProfile(customer) });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = FirstError() });
        var customer = await CurrentCustomerAsync();
        if (customer == null) return NotFound(new { success = false, message = "حساب المستخدم غير موجود." });

        if (string.IsNullOrEmpty(customer.PasswordHash) || !_passwordHasher.VerifyPassword(request.CurrentPassword, customer.PasswordHash))
        {
            return BadRequest(new { success = false, message = "كلمة المرور الحالية غير صحيحة." });
        }
        if (request.NewPassword == request.CurrentPassword)
        {
            return BadRequest(new { success = false, message = "اختر كلمة مرور مختلفة عن الحالية." });
        }

        customer.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        customer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم تغيير كلمة المرور." });
    }

    /// <summary>
    /// حذف الحساب بطلب العميل: تُمسح بياناته الشخصية ويُغلق الحساب نهائياً،
    /// وتبقى سجلات الفواتير والنقاط (بدون هوية) لأغراض المحاسبة ومكافحة الاحتيال.
    /// </summary>
    [HttpPost("delete-account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = FirstError() });
        var customer = await CurrentCustomerAsync();
        if (customer == null) return NotFound(new { success = false, message = "حساب المستخدم غير موجود." });

        if (string.IsNullOrEmpty(customer.PasswordHash) || !_passwordHasher.VerifyPassword(request.Password, customer.PasswordHash))
        {
            return BadRequest(new { success = false, message = "كلمة المرور غير صحيحة." });
        }

        CustomerAccounts.Anonymize(customer);
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "CUSTOMER_SELF_DELETE",
            EntityName = "Customer",
            EntityId = customer.CustomerId,
            PerformedBy = "العميل (من التطبيق)",
            Details = $"حذف العميل حسابه ({customer.MembershipNumber}).",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم حذف حسابك." });
    }

    private string FirstError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? "البيانات المدخلة غير صالحة.";
}

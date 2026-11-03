using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        var normalizedPhone = request.PhoneNumber.Trim();

        var phoneExists = await _context.Customers.AnyAsync(c => c.PhoneNumber == normalizedPhone);
        if (phoneExists)
        {
            return Conflict(new
            {
                success = false,
                message = "رقم الهاتف مسجل مسبقاً في النظام (Phone number already registered)."
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
        await _context.SaveChangesAsync();

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

        var normalizedPhone = request.PhoneNumber.Trim();

        // 1. أولاً فحص إذا كان موظف/مسؤول نظام في SystemUsers
        var staffUser = await _context.SystemUsers
            .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone || u.Email == normalizedPhone);

        if (staffUser != null)
        {
            var isValidStaffPassword = _passwordHasher.VerifyPassword(request.Password, staffUser.PasswordHash);
            if (isValidStaffPassword && staffUser.IsActive)
            {
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
            .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhone);

        if (customer == null || customer.IsFrozen)
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

        var profile = new UserProfileDto
        {
            Id = customer.CustomerId,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            Role = "CUSTOMER",
            PointsBalance = (int)(customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS)?.CurrentBalance ?? 0),
            CreatedAt = customer.RegisteredAt
        };

        return Ok(profile);
    }
}

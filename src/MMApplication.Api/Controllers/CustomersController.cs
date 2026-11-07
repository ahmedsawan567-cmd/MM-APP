using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Application.DTOs;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

using MMApplication.Api.Security;
using MMApplication.Infrastructure.Security;

namespace MMApplication.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = StaffRoles.All)]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(AppDbContext context, ILogger<CustomersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// استعلام قائمة العملاء مع أرصدة محافظهم.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.CustomersView)]
    [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllCustomers()
    {
        var customers = await _context.Customers
            .Include(c => c.Wallets)
            .AsNoTracking()
            .Select(c => new CustomerDto
            {
                Id = c.CustomerId,
                FullName = c.FullName,
                PhoneNumber = c.PhoneNumber,
                PointsBalance = (int)(c.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS) != null ? c.Wallets.First(w => w.BalanceType == BalanceType.POINTS).CurrentBalance : 0),
                CreatedAt = c.RegisteredAt
            })
            .ToListAsync();

        return Ok(customers);
    }

    /// <summary>
    /// استعلام تفاصيل عميل محدد بالمعرف.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(Permissions.CustomersView)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerById(string id)
    {
        var customer = await _context.Customers
            .Include(c => c.Wallets)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
        {
            return NotFound(new { success = false, message = $"العميل بالمعرف '{id}' غير موجود." });
        }

        var dto = new CustomerDto
        {
            Id = customer.CustomerId,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            PointsBalance = (int)(customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS)?.CurrentBalance ?? 0),
            CreatedAt = customer.RegisteredAt
        };

        return Ok(dto);
    }

    /// <summary>
    /// تسجيل عميل جديد مع إنشاء محفظته برصيد 0.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.CustomersEdit)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            return BadRequest(new { success = false, message = "الاسم الكامل ورقم الهاتف حقول مطلوبة." });
        }

        var phoneExists = await _context.Customers.AnyAsync(c => c.PhoneNumber == dto.PhoneNumber.Trim());
        if (phoneExists)
        {
            return Conflict(new { success = false, message = "رقم الهاتف مسجل مسبقاً لعميل آخر." });
        }

        var customerId = string.IsNullOrWhiteSpace(dto.Id) ? $"CUST-{Guid.NewGuid().ToString()[..8].ToUpper()}" : dto.Id.Trim();

        var idExists = await _context.Customers.AnyAsync(c => c.CustomerId == customerId);
        if (idExists)
        {
            return Conflict(new { success = false, message = "معرف العميل (Id) مستخدم بالفعل." });
        }

        var customer = new Customer
        {
            CustomerId = customerId,
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
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

        _logger.LogInformation("تم إنشاء عميل جديد بنجاح: {CustomerId} ({FullName})", customer.CustomerId, customer.FullName);

        var result = new CustomerDto
        {
            Id = customer.CustomerId,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            PointsBalance = (int)wallet.CurrentBalance,
            CreatedAt = customer.RegisteredAt
        };

        return CreatedAtAction(nameof(GetCustomerById), new { id = customer.CustomerId }, result);
    }
}

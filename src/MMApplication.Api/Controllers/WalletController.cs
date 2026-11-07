using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Application.DTOs;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

using MMApplication.Api.Security;
using MMApplication.Infrastructure.Security;

namespace MMApplication.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WalletController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<WalletController> _logger;

    public WalletController(AppDbContext context, ILogger<WalletController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// استعلام ملخص المحفظة والنقاط والقسائم النشطة للعميل الحالي.
    /// </summary>
    [HttpGet("summary")]
    [Authorize]
    [ProducesResponseType(typeof(WalletSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWalletSummary()
    {
        var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized(new { success = false, message = "غير مصرح." });
        }

        var customer = await _context.Customers
            .Include(c => c.Wallets)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == currentUserId);

        if (customer == null)
        {
            return NotFound(new { success = false, message = "المحفظة غير موجودة." });
        }

        var pointsWallet = customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS);

        var totalEarned = (int)await _context.WalletLedger
            .Where(t => t.Wallet.CustomerId == currentUserId && t.AmountChanged > 0)
            .SumAsync(t => t.AmountChanged);

        var totalSpent = (int)await _context.WalletLedger
            .Where(t => t.Wallet.CustomerId == currentUserId && t.AmountChanged < 0)
            .SumAsync(t => Math.Abs(t.AmountChanged));

        var activeVouchersCount = await _context.Redemptions
            .CountAsync(r => r.CustomerId == currentUserId && r.Status == RedemptionStatus.ACTIVE && r.ExpiresAt > DateTime.UtcNow);

        var summary = new WalletSummaryDto
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.FullName,
            PointsBalance = (int)(pointsWallet?.CurrentBalance ?? 0),
            TotalPointsEarned = totalEarned,
            TotalPointsSpent = totalSpent,
            ActiveVouchersCount = activeVouchersCount,
            LastUpdated = DateTime.UtcNow
        };

        return Ok(summary);
    }

    /// <summary>
    /// استعلام سجل حركات النقاط التفصيلي للعميل الحالي (كسب، استبدال، مكافآت).
    /// </summary>
    [HttpGet("transactions")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<PointTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPointTransactions()
    {
        var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized(new { success = false, message = "غير مصرح." });
        }

        var transactions = await _context.WalletLedger
            .Where(t => t.Wallet.CustomerId == currentUserId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PointTransactionDto
            {
                Id = Guid.NewGuid(),
                CustomerId = currentUserId,
                Amount = (int)t.AmountChanged,
                BalanceAfter = (int)t.BalanceAfter,
                Type = t.TransactionType.ToString(),
                Description = t.ReferenceRemarks ?? t.TransactionType.ToString(),
                ReferenceId = t.InvoiceId,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return Ok(transactions);
    }

    /// <summary>
    /// استعراض قسائم وكوبونات العميل المستبدلة (النشطة والمستخدمة والمنتهية).
    /// </summary>
    [HttpGet("my-vouchers")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<VoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyVouchers()
    {
        var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized(new { success = false, message = "غير مصرح." });
        }

        var vouchers = await _context.Redemptions
            .Include(r => r.Reward)
            .Where(r => r.CustomerId == currentUserId)
            .OrderByDescending(r => r.RedeemedAt)
            .Select(r => new VoucherDto
            {
                Id = r.Id,
                RedemptionCode = r.RedemptionCode,
                RewardId = r.RewardId,
                RewardTitle = r.Reward.Title,
                MerchantName = r.Reward.MerchantName,
                DiscountValue = r.Reward.DiscountValue,
                PointsSpent = r.PointsSpent,
                Status = r.Status.ToString(),
                RedeemedAt = r.RedeemedAt,
                UsedAt = r.UsedAt,
                UsedByMerchant = r.UsedByMerchant,
                ExpiresAt = r.ExpiresAt
            })
            .ToListAsync();

        return Ok(vouchers);
    }

    /// <summary>
    /// التحقق من صلاحية قسيمة الخصم واستخدامها عند الكاشير (للتجار والمدراء).
    /// </summary>
    [HttpPost("vouchers/{code}/validate-and-use")]
    [RequirePermission(Permissions.VouchersRedeem)]
    [Authorize(Roles = "Admin,Merchant,SUPER_ADMIN,STORE_MANAGER,CASHIER,FRAUD_AUDITOR")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateAndUseVoucher(string code, [FromBody] UseVoucherRequestDto? request)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { success = false, message = "كود القسيمة مطلوب." });
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        var redemption = await _context.Redemptions
            .Include(r => r.Reward)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.RedemptionCode == normalizedCode);

        if (redemption == null)
        {
            return NotFound(new { success = false, message = $"القسيمة بالكود '{normalizedCode}' غير موجودة." });
        }

        if (redemption.Status == RedemptionStatus.USED)
        {
            return BadRequest(new
            {
                success = false,
                message = $"تم استخدام هذه القسيمة مسبقاً بتاريخ {redemption.UsedAt:yyyy-MM-dd HH:mm} لدى: {redemption.UsedByMerchant ?? "المتجر"}.",
                usedAt = redemption.UsedAt,
                usedByMerchant = redemption.UsedByMerchant
            });
        }

        if (redemption.Status == RedemptionStatus.EXPIRED || DateTime.UtcNow > redemption.ExpiresAt)
        {
            redemption.Status = RedemptionStatus.EXPIRED;
            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                success = false,
                message = $"هذه القسيمة منتهية الصلاحية منذ تاريخ {redemption.ExpiresAt:yyyy-MM-dd HH:mm}.",
                expiresAt = redemption.ExpiresAt
            });
        }

        if (redemption.Status != RedemptionStatus.ACTIVE)
        {
            return BadRequest(new
            {
                success = false,
                message = $"حالة القسيمة غير صالحة للاستخدام ({redemption.Status})."
            });
        }

        var merchantName = request?.MerchantName
            ?? User?.FindFirst(ClaimTypes.Name)?.Value
            ?? "الكاشير";

        redemption.Status = RedemptionStatus.USED;
        redemption.UsedAt = DateTime.UtcNow;
        redemption.UsedByMerchant = merchantName;

        await _context.SaveChangesAsync();

        _logger.LogInformation("تم استخدام القسيمة {Code} بنجاح لدى {Merchant} للعميل {Customer}",
            redemption.RedemptionCode, merchantName, redemption.Customer?.FullName);

        return Ok(new
        {
            success = true,
            message = "تم التحقق من القسيمة وتطبيق الخصم بنجاح (Voucher applied successfully).",
            voucherCode = redemption.RedemptionCode,
            rewardTitle = redemption.Reward.Title,
            discountValue = redemption.Reward.DiscountValue,
            customerName = redemption.Customer?.FullName ?? "-",
            usedAt = redemption.UsedAt,
            usedByMerchant = redemption.UsedByMerchant
        });
    }
}

using System.Security.Claims;
using System.Security.Cryptography;
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
public class RewardsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<RewardsController> _logger;

    public RewardsController(AppDbContext context, ILogger<RewardsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// استعراض قائمة المكافآت والعروض المتاحة للاستبدال.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RewardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRewards([FromQuery] bool onlyActive = true)
    {
        var query = _context.Rewards.AsNoTracking();

        if (onlyActive)
        {
            query = query.Where(r => r.IsActive && (!r.ValidUntil.HasValue || r.ValidUntil > DateTime.UtcNow) && (!r.StockQuantity.HasValue || r.StockQuantity > 0));
        }

        var list = await query
            .OrderBy(r => r.PointsCost)
            .Select(r => new RewardDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                MerchantName = r.MerchantName,
                PointsCost = r.PointsCost,
                DiscountValue = r.DiscountValue,
                ImageUrl = r.ImageUrl,
                StockQuantity = r.StockQuantity,
                IsActive = r.IsActive,
                ValidityDays = r.ValidityDays,
                ValidUntil = r.ValidUntil,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>
    /// استعلام تفاصيل مكافأة معينة بالمعرف.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RewardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRewardById(Guid id)
    {
        var reward = await _context.Rewards.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (reward == null)
        {
            return NotFound(new { success = false, message = "المكافأة غير موجودة." });
        }

        var dto = new RewardDto
        {
            Id = reward.Id,
            Title = reward.Title,
            Description = reward.Description,
            MerchantName = reward.MerchantName,
            PointsCost = reward.PointsCost,
            DiscountValue = reward.DiscountValue,
            ImageUrl = reward.ImageUrl,
            StockQuantity = reward.StockQuantity,
            IsActive = reward.IsActive,
            ValidityDays = reward.ValidityDays,
            ValidUntil = reward.ValidUntil,
            CreatedAt = reward.CreatedAt
        };

        return Ok(dto);
    }

    /// <summary>
    /// إنشاء مكافأة جديدة في النظام (للمدراء فقط).
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.RewardsManage)]
    [Authorize(Roles = "Admin,SUPER_ADMIN,STORE_MANAGER")]
    [ProducesResponseType(typeof(RewardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateReward([FromBody] CreateRewardDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var reward = new Reward
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            MerchantName = dto.MerchantName?.Trim(),
            PointsCost = dto.PointsCost,
            DiscountValue = dto.DiscountValue,
            ImageUrl = dto.ImageUrl,
            StockQuantity = dto.StockQuantity,
            IsActive = true,
            ValidityDays = dto.ValidityDays,
            ValidUntil = dto.ValidUntil,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Rewards.AddAsync(reward);
        await _context.SaveChangesAsync();

        _logger.LogInformation("تم إنشاء مكافأة جديدة: {RewardId} ({Title})", reward.Id, reward.Title);

        var result = new RewardDto
        {
            Id = reward.Id,
            Title = reward.Title,
            Description = reward.Description,
            MerchantName = reward.MerchantName,
            PointsCost = reward.PointsCost,
            DiscountValue = reward.DiscountValue,
            ImageUrl = reward.ImageUrl,
            StockQuantity = reward.StockQuantity,
            IsActive = reward.IsActive,
            ValidityDays = reward.ValidityDays,
            ValidUntil = reward.ValidUntil,
            CreatedAt = reward.CreatedAt
        };

        return CreatedAtAction(nameof(GetRewardById), new { id = reward.Id }, result);
    }

    /// <summary>
    /// استبدال النقاط بمكافأة وتوليد قسيمة كود خصم للعميل الحالي المسجل.
    /// </summary>
    [HttpPost("{id:guid}/redeem")]
    [Authorize]
    [ProducesResponseType(typeof(RedemptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RedeemReward(Guid id)
    {
        var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized(new { success = false, message = "غير مصرح (Unauthorized)." });
        }

        var customer = await _context.Customers
            .Include(c => c.Wallets)
            .FirstOrDefaultAsync(c => c.CustomerId == currentUserId);

        if (customer == null)
        {
            return NotFound(new { success = false, message = "حساب العميل غير موجود." });
        }

        var wallet = customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS);
        if (wallet == null)
        {
            return BadRequest(new { success = false, message = "المحفظة لا تحتوي على نقاط ولاء." });
        }

        var reward = await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id);
        if (reward == null || !reward.IsActive)
        {
            return NotFound(new { success = false, message = "المكافأة المطلوبة غير متاحة حالياً." });
        }

        if (reward.ValidUntil.HasValue && reward.ValidUntil.Value < DateTime.UtcNow)
        {
            return BadRequest(new { success = false, message = "انتهت فترة صلاحية هذه المكافأة." });
        }

        if (reward.StockQuantity.HasValue && reward.StockQuantity.Value <= 0)
        {
            return BadRequest(new { success = false, message = "نفدت الكمية المتاحة من هذه المكافأة (Out of stock)." });
        }

        if (wallet.CurrentBalance < reward.PointsCost)
        {
            return BadRequest(new
            {
                success = false,
                message = $"رصيد نقاطك غير كافٍ. النقاط المطلوبة: {reward.PointsCost}، رصيدك الحالي: {wallet.CurrentBalance}.",
                requiredPoints = reward.PointsCost,
                currentBalance = wallet.CurrentBalance
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        RewardRedemption redemption = null!;
        int remainingBalance = 0;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                wallet.CurrentBalance -= reward.PointsCost;
                remainingBalance = (int)wallet.CurrentBalance;

                if (reward.StockQuantity.HasValue)
                {
                    reward.StockQuantity -= 1;
                }

                var code = GenerateUniqueRedemptionCode();

                redemption = new RewardRedemption
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.CustomerId,
                    RewardId = reward.Id,
                    RedemptionCode = code,
                    PointsSpent = reward.PointsCost,
                    Status = RedemptionStatus.ACTIVE,
                    RedeemedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(reward.ValidityDays)
                };

                await _context.Redemptions.AddAsync(redemption);

                var ledgerEntry = new WalletLedger
                {
                    Wallet = wallet,
                    AmountChanged = -reward.PointsCost,
                    BalanceAfter = remainingBalance,
                    TransactionType = WalletTransactionType.REDEEM_PURCHASE,
                    ReferenceRemarks = $"استبدال مكافأة: {reward.Title} (كود: {code})",
                    CreatedAt = DateTime.UtcNow
                };

                await _context.WalletLedger.AddAsync(ledgerEntry);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "فشلت عملية استبدال المكافأة داخل المعاملة (Redemption transaction failed).");
                throw;
            }
        });

        _logger.LogInformation("تم استبدال المكافأة بنجاح للعميل {CustomerId}: {RedemptionCode}, الخصم: {Points}",
            customer.CustomerId, redemption.RedemptionCode, reward.PointsCost);

        var response = new RedemptionResponseDto
        {
            Success = true,
            Message = "تم استبدال المكافأة بنجاح وتوليد قسيمة الخصم (Voucher generated successfully).",
            RedemptionId = redemption.Id,
            RedemptionCode = redemption.RedemptionCode,
            RewardId = reward.Id,
            RewardTitle = reward.Title,
            MerchantName = reward.MerchantName,
            DiscountValue = reward.DiscountValue,
            PointsSpent = reward.PointsCost,
            CurrentWalletPointsBalance = remainingBalance,
            Status = redemption.Status.ToString(),
            RedeemedAt = redemption.RedeemedAt,
            ExpiresAt = redemption.ExpiresAt
        };

        return Ok(response);
    }

    private static string GenerateUniqueRedemptionCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var randomBytes = RandomNumberGenerator.GetBytes(6);
        var sb = new System.Text.StringBuilder("MM-");
        foreach (var b in randomBytes)
        {
            sb.Append(chars[b % chars.Length]);
        }
        return sb.ToString();
    }
}

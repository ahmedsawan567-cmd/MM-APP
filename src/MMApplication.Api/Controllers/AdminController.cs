using MMApplication.Infrastructure.Services;
using MMApplication.Api.Services;
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
[Authorize(Roles = StaffRoles.All)]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminController> _logger;

    private readonly InvoiceReviewService _review;

    public AdminController(AppDbContext context, ILogger<AdminController> logger, InvoiceReviewService? review = null)
    {
        _context = context;
        _logger = logger;
        _review = review ?? new InvoiceReviewService(context,
            new FlexibleLoyaltyEvaluationService(context),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceReviewService>.Instance);
    }

    private string Reviewer => User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "API";

    private async Task<IActionResult> InvoiceResult(Guid id, InvoiceReviewOutcome outcome)
    {
        if (!outcome.Success)
        {
            return StatusCode(outcome.StatusCode, new { success = false, message = outcome.Message });
        }
        var invoice = await _context.Invoices.AsNoTracking().FirstAsync(i => i.InvoiceId == id);
        return Ok(new InvoiceResponseDto
        {
            Id = invoice.InvoiceId,
            CustomerId = invoice.CustomerId,
            MerchantName = invoice.ExtractedMerchant ?? "متجر",
            InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
            InvoiceDate = invoice.InvoiceDate.GetValueOrDefault(invoice.CreatedAt),
            TotalAmount = invoice.TotalAmount.GetValueOrDefault(0m),
            PointsEarned = invoice.PointsAwarded,
            Status = invoice.ProcessingStatus.ToString(),
            RejectionReason = invoice.RejectionReason,
            ImageUrl = invoice.ReceiptImageUrl,
            CreatedAt = invoice.CreatedAt
        });
    }

    /// <summary>
    /// استعلام المقاييس والإحصائيات الشاملة للوحة تحكم الإدارة.
    /// </summary>
    [HttpGet("dashboard/metrics")]
    [RequirePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(DashboardMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardMetrics()
    {
        var totalCustomers = await _context.Customers.CountAsync();
        var totalInvoices = await _context.Invoices.CountAsync();
        var approvedInvoices = await _context.Invoices.CountAsync(i => i.ProcessingStatus == InvoiceStatus.APPROVED);
        var pendingInvoices = await _context.Invoices.CountAsync(i => i.ProcessingStatus == InvoiceStatus.PENDING || i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED);
        var rejectedInvoices = await _context.Invoices.CountAsync(i => i.ProcessingStatus == InvoiceStatus.REJECTED);

        var totalSales = await _context.Invoices
            .Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED)
            .SumAsync(i => i.TotalAmount) ?? 0m;

        var totalPointsIssued = (int)await _context.WalletLedger
            .Where(t => t.AmountChanged > 0)
            .SumAsync(t => t.AmountChanged);

        var totalPointsRedeemed = (int)await _context.WalletLedger
            .Where(t => t.AmountChanged < 0)
            .SumAsync(t => Math.Abs(t.AmountChanged));

        var activeVouchers = await _context.Redemptions
            .CountAsync(r => r.Status == RedemptionStatus.ACTIVE && r.ExpiresAt > DateTime.UtcNow);

        var totalRewards = await _context.Rewards.CountAsync();

        var metrics = new DashboardMetricsDto
        {
            TotalCustomers = totalCustomers,
            TotalInvoices = totalInvoices,
            ApprovedInvoicesCount = approvedInvoices,
            PendingInvoicesCount = pendingInvoices,
            RejectedInvoicesCount = rejectedInvoices,
            TotalSalesVolume = totalSales,
            TotalPointsIssued = totalPointsIssued,
            TotalPointsRedeemed = totalPointsRedeemed,
            ActiveVouchersCount = activeVouchers,
            TotalRewardsCount = totalRewards
        };

        return Ok(metrics);
    }

    /// <summary>
    /// استعلام المتاجر الأكثر نشاطاً ومبيعات من واقع الفواتير المعتمدة.
    /// </summary>
    [HttpGet("dashboard/top-merchants")]
    [RequirePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(IEnumerable<TopMerchantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopMerchants([FromQuery] int limit = 5)
    {
        var topMerchants = await _context.Invoices
            .Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED)
            .GroupBy(i => i.ExtractedMerchant ?? (i.Tenant != null ? i.Tenant.NameAr : "متجر"))
            .Select(g => new TopMerchantDto
            {
                MerchantName = g.Key,
                InvoicesCount = g.Count(),
                TotalSales = g.Sum(i => i.TotalAmount) ?? 0m,
                TotalPointsAwarded = g.Sum(i => i.PointsAwarded)
            })
            .OrderByDescending(m => m.TotalSales)
            .Take(limit)
            .ToListAsync();

        return Ok(topMerchants);
    }

    /// <summary>
    /// خلاصة أحدث الأنشطة والعمليات في النظام (فواتير، استبدال، تسجيل).
    /// </summary>
    [HttpGet("dashboard/recent-activity")]
    [RequirePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(IEnumerable<RecentActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentActivity([FromQuery] int limit = 10)
    {
        var activities = new List<RecentActivityDto>();

        // أحدث الفواتير
        var recentInvoices = await _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Tenant)
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .Select(i => new RecentActivityDto
            {
                ActivityType = "INVOICE",
                Title = "فاتورة: " + (i.Tenant != null ? i.Tenant.NameAr : (i.ExtractedMerchant ?? "متجر")),
                Description = "فاتورة بقيمة " + (i.TotalAmount ?? 0m).ToString("N2") + " د.ل (#" + i.InvoiceNumber + ") - الحالة: " + i.ProcessingStatus,
                CustomerName = i.Customer != null ? i.Customer.FullName : "-",
                Amount = i.TotalAmount,
                Points = i.PointsAwarded,
                Timestamp = i.CreatedAt
            })
            .ToListAsync();

        // أحدث حركات المحفظة
        var recentWalletEntries = await _context.WalletLedger
            .Include(w => w.Wallet)
                .ThenInclude(w => w.Customer)
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .Select(w => new RecentActivityDto
            {
                ActivityType = w.TransactionType.ToString(),
                Title = $"حركة محفظة: {w.TransactionType}",
                Description = $"{w.ReferenceRemarks} - التغيير: {w.AmountChanged}",
                CustomerName = w.Wallet.Customer != null ? w.Wallet.Customer.FullName : "-",
                Points = (int)w.AmountChanged,
                Timestamp = w.CreatedAt
            })
            .ToListAsync();

        activities.AddRange(recentInvoices);
        activities.AddRange(recentWalletEntries);

        var result = activities
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// استعراض قائمة الفواتير المعلّقة التي تتطلب مراجعة بشرية.
    /// </summary>
    [HttpGet("invoices/pending")]
    [RequirePermission(Permissions.InvoicesReview)]
    [ProducesResponseType(typeof(IEnumerable<InvoiceResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingInvoices()
    {
        var pending = await _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Tenant)
            .Where(i => i.ProcessingStatus == InvoiceStatus.PENDING || i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceResponseDto
            {
                Id = i.InvoiceId,
                CustomerId = i.CustomerId,
                MerchantName = i.Tenant != null ? i.Tenant.NameAr : (i.ExtractedMerchant ?? "متجر"),
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate.GetValueOrDefault(i.CreatedAt),
                TotalAmount = i.TotalAmount.GetValueOrDefault(0m),
                PointsEarned = i.PointsAwarded,
                Status = i.ProcessingStatus.ToString(),
                RejectionReason = i.RejectionReason,
                ImageUrl = i.ReceiptImageUrl,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return Ok(pending);
    }

    /// <summary>
    /// اعتماد فاتورة معلّقة يدوياً من الإدارة واحتساب النقاط للعميل.
    /// </summary>
    [HttpPost("invoices/{id:guid}/approve")]
    [RequirePermission(Permissions.InvoicesReview)]
    [ProducesResponseType(typeof(InvoiceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveInvoice(Guid id)
    {
        // الحساب بمحرك الولاء الموحّد (قواعد المتجر والعروض ومستوى العضوية)
        return await InvoiceResult(id, await _review.ApproveAsync(id, Reviewer));
    }

    /// <summary>
    /// رفض فاتورة معلّقة مع ذكر سبب الرفض.
    /// </summary>
    [HttpPost("invoices/{id:guid}/reject")]
    [RequirePermission(Permissions.InvoicesReview)]
    [ProducesResponseType(typeof(InvoiceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectInvoice(Guid id, [FromBody] RejectInvoiceRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // رفض فاتورة معتمدة يسترجع نقاطها تلقائياً
        return await InvoiceResult(id, await _review.RejectAsync(id, Reviewer, dto.Reason));
    }

    /// <summary>
    /// تعديل رصيد نقاط عميل يدوياً (إضافة أو خصم نقاط كبونص أو تصحيح).
    /// </summary>
    [HttpPost("customers/{id}/adjust-points")]
    [RequirePermission(Permissions.CustomersPoints)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustCustomerPoints(string id, [FromBody] AdjustPointsDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.PointsAmount == 0)
        {
            return BadRequest(new { success = false, message = "يجب أن تكون قيمة النقاط المعدلة غير صفرية." });
        }

        var customer = await _context.Customers
            .Include(c => c.Wallets)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
        {
            return NotFound(new { success = false, message = "العميل غير موجود." });
        }

        var wallet = customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS);
        if (wallet == null)
        {
            wallet = new CustomerWallet
            {
                CustomerId = customer.CustomerId,
                BalanceType = BalanceType.POINTS,
                CurrentBalance = 0
            };
            await _context.CustomerWallets.AddAsync(wallet);
        }

        if (wallet.CurrentBalance + dto.PointsAmount < 0)
        {
            return BadRequest(new
            {
                success = false,
                message = $"لا يمكن خصم {Math.Abs(dto.PointsAmount)} نقطة لأن الرصيد الحالي ({wallet.CurrentBalance}) سيصبح سالباً."
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                wallet.CurrentBalance += dto.PointsAmount;

                var ledgerEntry = new WalletLedger
                {
                    Wallet = wallet,
                    AmountChanged = dto.PointsAmount,
                    BalanceAfter = wallet.CurrentBalance,
                    TransactionType = WalletTransactionType.MANUAL_ADJUSTMENT,
                    ReferenceRemarks = $"تعديل إداري: {dto.Reason.Trim()}",
                    CreatedAt = DateTime.UtcNow
                };

                await _context.WalletLedger.AddAsync(ledgerEntry);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "فشل تعديل النقاط للعميل {CustomerId}", customer.CustomerId);
                throw;
            }
        });

        _logger.LogInformation("تم تعديل نقاط العميل {CustomerId} بمقدار {Amount}. الرصيد الجديد: {Balance}",
            customer.CustomerId, dto.PointsAmount, wallet.CurrentBalance);

        return Ok(new
        {
            success = true,
            message = "تم تعديل رصيد النقاط بنجاح.",
            customerId = customer.CustomerId,
            customerName = customer.FullName,
            adjustment = dto.PointsAmount,
            newPointsBalance = wallet.CurrentBalance,
            reason = dto.Reason
        });
    }

    /// <summary>
    /// استعراض قائمة العملاء بتفاصيل موسعة للإدارة (مع البحث والفلترة).
    /// </summary>
    [HttpGet("customers")]
    [RequirePermission(Permissions.CustomersView)]
    [ProducesResponseType(typeof(IEnumerable<AdminCustomerDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminCustomers([FromQuery] string? search)
    {
        var query = _context.Customers
            .Include(c => c.Wallets)
            .Include(c => c.Invoices)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c => c.FullName.Contains(s) || c.PhoneNumber.Contains(s) || c.CustomerId.Contains(s));
        }

        var list = await query
            .OrderByDescending(c => c.RegisteredAt)
            .Select(c => new AdminCustomerDetailsDto
            {
                Id = c.CustomerId,
                FullName = c.FullName,
                PhoneNumber = c.PhoneNumber,
                Role = "CUSTOMER",
                PointsBalance = (int)(c.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS) != null ? c.Wallets.First(w => w.BalanceType == BalanceType.POINTS).CurrentBalance : 0),
                InvoicesCount = c.Invoices.Count,
                RedemptionsCount = 0,
                CreatedAt = c.RegisteredAt
            })
            .ToListAsync();

        return Ok(list);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Controllers;

/// <summary>
/// كسب النقاط بباركود المتجر أو بطاقة NFC: نقاط ثابتة يحددها المول لكل متجر،
/// تُضاف فوراً عند المسح، بحد أقصى مرات يومياً لكل عميل في كل متجر.
/// </summary>
[ApiController]
[Route("api/v1/purchases")]
public class PurchaseClaimsController : ControllerBase
{
    public static readonly TimeSpan ConfirmWindow = TimeSpan.FromHours(24);

    private readonly AppDbContext _context;
    public PurchaseClaimsController(AppDbContext context) => _context = context;

    private string? CustomerId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

    /// <summary>بيانات المتجر من محتوى الباركود/البطاقة (قبل إدخال المبلغ).</summary>
    [HttpGet("store")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> ResolveStore([FromQuery] string code)
    {
        var storeCode = StoreCodes.Parse(code);
        var store = storeCode == null ? null : await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.StoreCode == storeCode);
        if (store == null || !store.IsActive)
            return NotFound(new { success = false, message = "هذا الكود لا يخص متجراً في المرأة مول." });
        return Ok(new
        {
            success = true,
            store = new { id = store.TenantId, nameAr = store.NameAr, nameEn = store.NameEn, category = store.Category, logoUrl = store.LogoUrl, unitNumber = store.UnitNumber },
            points = store.ScanPoints,
            dailyLimit = store.ScanDailyLimit,
            code = store.StoreCode,
        });
    }

    public record ClaimRequest(string? Code, string? Method);

    /// <summary>
    /// مسح باركود المتجر أو بطاقة NFC: يأخذ العميل نقاط المتجر المحددة من المنظومة فوراً،
    /// بحد أقصى عدد مرات يومياً لكل متجر (يحدده المول لكل متجر).
    /// </summary>
    [HttpPost("claim")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> Claim([FromBody] ClaimRequest req)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == CustomerId);
        if (customer == null || customer.IsFrozen || customer.IsArchived)
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "حسابك غير مفعّل لكسب النقاط. راجع خدمة العملاء." });

        var storeCode = StoreCodes.Parse(req.Code);
        var store = storeCode == null ? null : await _context.Tenants.FirstOrDefaultAsync(t => t.StoreCode == storeCode);
        if (store == null || !store.IsActive) return NotFound(new { success = false, message = "هذا الكود لا يخص متجراً في المرأة مول." });
        if (store.ScanPoints <= 0) return BadRequest(new { success = false, message = $"نقاط الباركود غير مفعّلة في {store.NameAr} حالياً." });

        // اليوم بتوقيت الخادم المحلي
        var dayStart = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Local).ToUniversalTime();
        var todayScans = await _context.Invoices.CountAsync(i => i.CustomerId == customer.CustomerId && i.TenantId == store.TenantId &&
                                                                  (i.Source == "QR" || i.Source == "NFC") && i.CreatedAt >= dayStart);
        if (todayScans >= Math.Max(1, store.ScanDailyLimit))
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                success = false,
                message = store.ScanDailyLimit <= 1
                    ? $"أخذت نقاط {store.NameAr} اليوم. يمكنك المسح مرة أخرى غداً."
                    : $"وصلت للحد اليومي ({store.ScanDailyLimit} مرات) في {store.NameAr}. عُد غداً.",
            });

        var method = string.Equals(req.Method, "NFC", StringComparison.OrdinalIgnoreCase) ? "NFC" : "QR";
        var points = store.ScanPoints;
        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            CustomerId = customer.CustomerId,
            TenantId = store.TenantId,
            Source = method,
            ExtractedMerchant = store.NameAr,
            InvoiceNumber = $"{method}-{now:yyMMdd}-{StoreCodes.NewCode()[..6]}",
            InvoiceDate = now,
            TotalAmount = 0,
            ConfidenceScore = 1m,
            ProcessingStatus = InvoiceStatus.APPROVED,
            PointsAwarded = points,
            BasePointsEarned = points,
            ReceiptImageUrl = string.Empty,
            CreatedAt = now,
        };
        _context.Invoices.Add(invoice);

        var wallet = await _context.CustomerWallets.FirstOrDefaultAsync(w => w.CustomerId == customer.CustomerId && w.BalanceType == BalanceType.POINTS);
        if (wallet == null)
        {
            wallet = new CustomerWallet { CustomerId = customer.CustomerId, BalanceType = BalanceType.POINTS, CurrentBalance = 0 };
            _context.CustomerWallets.Add(wallet);
        }
        wallet.CurrentBalance += points;
        _context.WalletLedger.Add(new WalletLedger
        {
            Wallet = wallet,
            InvoiceId = invoice.InvoiceId,
            AmountChanged = points,
            BalanceAfter = wallet.CurrentBalance,
            TransactionType = WalletTransactionType.EARN_RECEIPT,
            ReferenceRemarks = $"نقاط {(method == "NFC" ? "بطاقة" : "باركود")} {store.NameAr}",
            CreatedAt = now,
        });
        Notifier.Customer(_context, customer.CustomerId, "INVOICE_APPROVED", $"حصلت على {points} نقطة",
            $"من {(method == "NFC" ? "بطاقة" : "باركود")} {store.NameAr}. رصيدك الآن {wallet.CurrentBalance:N0} نقطة.", $"invoice:{invoice.InvoiceId}");
        await _context.SaveChangesAsync();
        await MembershipTiers.EvaluateAsync(_context, customer.CustomerId);

        return Ok(new
        {
            success = true,
            message = $"حصلت على {points} نقطة من {store.NameAr}",
            points,
            newBalance = (int)wallet.CurrentBalance,
            storeName = store.NameAr,
            remainingToday = Math.Max(0, store.ScanDailyLimit - todayScans - 1),
            invoiceId = invoice.InvoiceId,
        });
    }
}

/// <summary>منطق طلبات الباركود/NFC المشترك.</summary>
public static class StoreClaimsService
{
    /// <summary>الطلبات التي لم يؤكدها المتجر خلال المهلة تُرفض تلقائياً.</summary>
    public static async Task<int> ExpireOldAsync(AppDbContext db)
    {
        var cutoff = DateTime.UtcNow - PurchaseClaimsController.ConfirmWindow;
        var old = await db.Invoices.Where(i => i.ProcessingStatus == InvoiceStatus.AWAITING_STORE && i.CreatedAt < cutoff).ToListAsync();
        foreach (var i in old)
        {
            i.ProcessingStatus = InvoiceStatus.REJECTED;
            i.RejectionReason = "انتهت مهلة تأكيد المتجر (24 ساعة).";
            Notifier.Customer(db, i.CustomerId, "CLAIM_EXPIRED", "لم يُؤكَّد طلب النقاط",
                $"انتهت مهلة تأكيد طلبك في {i.ExtractedMerchant}. يمكنك طلب ذلك من الكاشير عند زيارتك القادمة.", $"invoice:{i.InvoiceId}");
        }
        if (old.Count > 0) await db.SaveChangesAsync();
        return old.Count;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Controllers;

/// <summary>
/// كسب النقاط بباركود المتجر أو بطاقة NFC (للمتاجر التي لا تصدر فواتير):
/// العميل يمسح كود المتجر ويدخل مبلغ الشراء، والطلب ينتظر تأكيد كاشير المتجر قبل إضافة النقاط.
/// </summary>
[ApiController]
[Route("api/v1/purchases")]
public class PurchaseClaimsController : ControllerBase
{
    public const int MaxClaimsPerStorePerDay = 3;
    public const decimal MaxAmount = 10000m;
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
            code = store.StoreCode,
        });
    }

    public record ClaimRequest(string? Code, decimal Amount, string? Method, string? Note);

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

        var amount = Math.Round(req.Amount, 2);
        if (amount <= 0) return BadRequest(new { success = false, message = "أدخل مبلغ الشراء." });
        if (amount > MaxAmount) return BadRequest(new { success = false, message = $"المبلغ أكبر من الحد المسموح ({MaxAmount:N0} د.ل). صوّر الفاتورة بدلاً من ذلك." });

        await StoreClaimsService.ExpireOldAsync(_context);

        var since = DateTime.UtcNow.AddHours(-24);
        var recent = await _context.Invoices.Where(i => i.CustomerId == customer.CustomerId && i.TenantId == store.TenantId &&
                                                         (i.Source == "QR" || i.Source == "NFC") && i.CreatedAt >= since).ToListAsync();
        if (recent.Any(i => i.ProcessingStatus == InvoiceStatus.AWAITING_STORE))
            return Conflict(new { success = false, message = "لديك طلب سابق في هذا المتجر بانتظار تأكيد الكاشير." });
        if (recent.Count >= MaxClaimsPerStorePerDay)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = $"وصلت للحد اليومي ({MaxClaimsPerStorePerDay} طلبات) في هذا المتجر." });

        var method = string.Equals(req.Method, "NFC", StringComparison.OrdinalIgnoreCase) ? "NFC" : "QR";
        var invoice = new Invoice
        {
            CustomerId = customer.CustomerId,
            TenantId = store.TenantId,
            Source = method,
            ExtractedMerchant = store.NameAr,
            InvoiceNumber = $"{method}-{DateTime.UtcNow:yyMMdd}-{StoreCodes.NewCode()[..6]}",
            InvoiceDate = DateTime.UtcNow,
            TotalAmount = amount,
            ConfidenceScore = 1m,
            ProcessingStatus = InvoiceStatus.AWAITING_STORE,
            ReceiptImageUrl = string.Empty,
            ResubmissionNotes = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim()[..Math.Min(200, req.Note.Trim().Length)],
            CreatedAt = DateTime.UtcNow,
        };
        _context.Invoices.Add(invoice);
        Notifier.Customer(_context, customer.CustomerId, "CLAIM_SENT", "أُرسل طلبك للمتجر",
            $"طلب نقاط على مشتريات {amount:N2} د.ل في {store.NameAr}. تُضاف النقاط بعد تأكيد الكاشير.", $"invoice:{invoice.InvoiceId}");
        await Notifier.StaffAsync(_context, "CLAIM_PENDING", "طلب نقاط بانتظار تأكيدك",
            $"{customer.FullName} — {amount:N2} د.ل ({(method == "NFC" ? "بطاقة NFC" : "باركود المتجر")})", "/AdminDashboard/StoreClaims", store.TenantId, storeOnly: true);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "أُرسل طلبك. تُضاف النقاط بعد أن يؤكد كاشير المتجر المبلغ.",
            invoiceId = invoice.InvoiceId,
            storeName = store.NameAr,
            amount,
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

using Microsoft.EntityFrameworkCore;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Services;

/// <summary>تصحيحات يدخلها المراجع على ما قرأه الذكاء الاصطناعي قبل الاعتماد.</summary>
public class InvoiceCorrection
{
    public string? TenantId { get; set; }
    public string? MerchantName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public decimal? TotalAmount { get; set; }
}

public record InvoiceReviewOutcome(bool Success, string Message, int PointsChanged = 0, int StatusCode = 200)
{
    public static InvoiceReviewOutcome Fail(string message, int statusCode = 400) => new(false, message, 0, statusCode);
}

/// <summary>
/// منطق مراجعة الفواتير الموحّد (لوحة التحكم + واجهة الإدارة):
/// الاعتماد يحسب النقاط بنفس محرك الولاء المستخدم عند الرفع (قواعد المتجر، العروض، مستوى العضوية)،
/// والرفض بعد الاعتماد يسترجع النقاط، وكل قرار يُسجّل في سجل العمليات.
/// </summary>
public class InvoiceReviewService
{
    private readonly AppDbContext _context;
    private readonly IFlexibleLoyaltyEvaluationService _loyalty;
    private readonly ILogger<InvoiceReviewService> _logger;

    public InvoiceReviewService(AppDbContext context, IFlexibleLoyaltyEvaluationService loyalty, ILogger<InvoiceReviewService> logger)
    {
        _context = context;
        _loyalty = loyalty;
        _logger = logger;
    }

    public async Task<InvoiceReviewOutcome> ApproveAsync(Guid invoiceId, string reviewer, InvoiceCorrection? correction = null)
    {
        var invoice = await _context.Invoices.Include(i => i.Customer).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        if (invoice == null) return InvoiceReviewOutcome.Fail("الفاتورة غير موجودة.", 404);
        if (invoice.ProcessingStatus == InvoiceStatus.APPROVED) return InvoiceReviewOutcome.Fail("الفاتورة معتمدة مسبقاً.");
        if (invoice.ProcessingStatus == InvoiceStatus.VOIDED) return InvoiceReviewOutcome.Fail("لا يمكن اعتماد فاتورة ملغاة.");

        var changes = new List<string>();
        if (correction != null)
        {
            if (!string.IsNullOrWhiteSpace(correction.TenantId) && correction.TenantId != invoice.TenantId)
            {
                var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.TenantId == correction.TenantId);
                if (tenant == null) return InvoiceReviewOutcome.Fail("المتجر المحدد غير موجود.");
                changes.Add($"المتجر: {invoice.TenantId ?? "-"} ← {tenant.TenantId}");
                invoice.TenantId = tenant.TenantId;
                invoice.ExtractedMerchant ??= tenant.NameAr;
            }
            if (!string.IsNullOrWhiteSpace(correction.MerchantName) && correction.MerchantName.Trim() != invoice.ExtractedMerchant)
            {
                changes.Add($"اسم المتجر: {invoice.ExtractedMerchant} ← {correction.MerchantName.Trim()}");
                invoice.ExtractedMerchant = correction.MerchantName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(correction.InvoiceNumber) && correction.InvoiceNumber.Trim() != invoice.InvoiceNumber)
            {
                changes.Add($"رقم الفاتورة: {invoice.InvoiceNumber} ← {correction.InvoiceNumber.Trim()}");
                invoice.InvoiceNumber = correction.InvoiceNumber.Trim();
            }
            if (correction.InvoiceDate.HasValue && correction.InvoiceDate.Value.Date != invoice.InvoiceDate?.Date)
            {
                changes.Add($"التاريخ: {invoice.InvoiceDate:yyyy-MM-dd} ← {correction.InvoiceDate:yyyy-MM-dd}");
                invoice.InvoiceDate = DateTime.SpecifyKind(correction.InvoiceDate.Value.Date, DateTimeKind.Utc);
            }
            if (correction.TotalAmount.HasValue && correction.TotalAmount != invoice.TotalAmount)
            {
                if (correction.TotalAmount <= 0) return InvoiceReviewOutcome.Fail("المبلغ يجب أن يكون أكبر من صفر.");
                changes.Add($"المبلغ: {invoice.TotalAmount:N2} ← {correction.TotalAmount:N2}");
                invoice.TotalAmount = correction.TotalAmount;
            }
        }

        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber) || invoice.TotalAmount is not > 0)
        {
            return InvoiceReviewOutcome.Fail("أدخل رقم الفاتورة والمبلغ قبل الاعتماد.");
        }

        // منع استخدام نفس الفاتورة مرتين (نفس المتجر ونفس الرقم بصيغته الموحّدة، بأي حالة)
        var duplicate = await InvoiceKeys.FindDuplicateAsync(_context, invoice.TenantId,
            MMApplication.Application.Common.InvoiceNumbers.Key(invoice.InvoiceNumber), invoice.TotalAmount, invoice.InvoiceDate, invoice.InvoiceId, invoice.ExtractedMerchant);
        if (duplicate != null)
        {
            return InvoiceReviewOutcome.Fail(
                $"رقم الفاتورة مستخدم في فاتورة أخرى لنفس المتجر (العميل {duplicate.CustomerId}، الحالة {duplicate.ProcessingStatus}). لا يمكن اعتمادها.", 409);
        }

        var evaluation = await _loyalty.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = invoice.TotalAmount.Value,
            TenantId = invoice.TenantId,
            MerchantName = invoice.ExtractedMerchant ?? string.Empty,
            CustomerTier = invoice.Customer?.LoyaltyTier ?? "BRONZE",
            InvoiceDate = invoice.InvoiceDate ?? invoice.CreatedAt,
        });
        var points = evaluation.TotalPointsAwarded;

        await InTransactionAsync(async () =>
        {
            invoice.ProcessingStatus = InvoiceStatus.APPROVED;
            invoice.PointsAwarded = points;
            invoice.BasePointsEarned = evaluation.BasePointsEarned;
            invoice.ItemBonusPointsEarned = evaluation.ItemBonusPointsEarned;
            invoice.PointsBreakdownJson = evaluation.PointsBreakdownJson;
            invoice.CashbackAwarded = evaluation.CashbackAwarded;
            invoice.RejectionReason = null;

            if (points > 0)
            {
                var wallet = await GetOrCreatePointsWalletAsync(invoice);
                wallet.CurrentBalance += points;
                _context.WalletLedger.Add(new WalletLedger
                {
                    Wallet = wallet,
                    InvoiceId = invoice.InvoiceId,
                    AmountChanged = points,
                    BalanceAfter = wallet.CurrentBalance,
                    TransactionType = WalletTransactionType.EARN_RECEIPT,
                    ReferenceRemarks = $"كسب نقاط من فاتورة (اعتماد بعد المراجعة): {invoice.ExtractedMerchant} (#{invoice.InvoiceNumber})",
                    CreatedAt = DateTime.UtcNow,
                });
            }

            Audit(reviewer, "INVOICE_APPROVE", invoice,
                $"اعتماد الفاتورة {invoice.InvoiceNumber} ومنح {points} نقطة." +
                (changes.Count > 0 ? " تصحيحات: " + string.Join("، ", changes) : string.Empty));
        });

        _logger.LogInformation("Invoice {InvoiceId} approved by {Reviewer}: +{Points} points", invoice.InvoiceId, reviewer, points);
        return new InvoiceReviewOutcome(true, $"تم اعتماد الفاتورة وإضافة {points} نقطة للعميل.", points);
    }

    public async Task<InvoiceReviewOutcome> RejectAsync(Guid invoiceId, string reviewer, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3) return InvoiceReviewOutcome.Fail("اكتب سبب الرفض.");
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        if (invoice == null) return InvoiceReviewOutcome.Fail("الفاتورة غير موجودة.", 404);
        if (invoice.ProcessingStatus == InvoiceStatus.REJECTED) return InvoiceReviewOutcome.Fail("الفاتورة مرفوضة مسبقاً.");

        var reversed = 0;
        await InTransactionAsync(async () =>
        {
            // فاتورة معتمدة سابقاً: نسترجع نقاطها
            if (invoice.ProcessingStatus == InvoiceStatus.APPROVED && invoice.PointsAwarded > 0)
            {
                var wallet = await GetOrCreatePointsWalletAsync(invoice);
                reversed = invoice.PointsAwarded;
                wallet.CurrentBalance -= reversed;
                _context.WalletLedger.Add(new WalletLedger
                {
                    Wallet = wallet,
                    InvoiceId = invoice.InvoiceId,
                    AmountChanged = -reversed,
                    BalanceAfter = wallet.CurrentBalance,
                    TransactionType = WalletTransactionType.MANUAL_ADJUSTMENT,
                    ReferenceRemarks = $"استرجاع نقاط فاتورة مرفوضة بعد التدقيق: {invoice.ExtractedMerchant} (#{invoice.InvoiceNumber})",
                    CreatedAt = DateTime.UtcNow,
                });
                invoice.PointsAwarded = 0;
                invoice.CashbackAwarded = 0;
            }

            invoice.ProcessingStatus = InvoiceStatus.REJECTED;
            invoice.RejectionReason = reason.Trim();
            Audit(reviewer, "INVOICE_REJECT", invoice,
                $"رفض الفاتورة {invoice.InvoiceNumber}. السبب: {reason.Trim()}" + (reversed > 0 ? $" — استرجاع {reversed} نقطة." : string.Empty));
        });

        return new InvoiceReviewOutcome(true,
            reversed > 0 ? $"تم رفض الفاتورة واسترجاع {reversed} نقطة من رصيد العميل." : "تم رفض الفاتورة.", -reversed);
    }

    public async Task<InvoiceReviewOutcome> RequestResubmissionAsync(Guid invoiceId, string reviewer, string note)
    {
        if (string.IsNullOrWhiteSpace(note)) return InvoiceReviewOutcome.Fail("اكتب ما المطلوب من العميل.");
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        if (invoice == null) return InvoiceReviewOutcome.Fail("الفاتورة غير موجودة.", 404);
        if (invoice.ProcessingStatus == InvoiceStatus.APPROVED) return InvoiceReviewOutcome.Fail("لا يمكن طلب إعادة تصوير فاتورة معتمدة.");

        invoice.ProcessingStatus = InvoiceStatus.RESUBMISSION_REQUESTED;
        invoice.RejectionReason = note.Trim();
        invoice.ResubmissionNotes = note.Trim();
        invoice.ResubmissionRequestedAt = DateTime.UtcNow;
        Audit(reviewer, "INVOICE_REQUEST_RESUBMISSION", invoice, $"طلب إعادة تصوير الفاتورة {invoice.InvoiceNumber}: {note.Trim()}");
        await _context.SaveChangesAsync();
        return new InvoiceReviewOutcome(true, "تم إرسال طلب إعادة التصوير للعميل.");
    }

    private async Task<CustomerWallet> GetOrCreatePointsWalletAsync(Invoice invoice)
    {
        var wallet = await _context.CustomerWallets
            .FirstOrDefaultAsync(w => w.CustomerId == invoice.CustomerId && w.BalanceType == BalanceType.POINTS);
        if (wallet != null) return wallet;

        wallet = new CustomerWallet { CustomerId = invoice.CustomerId, BalanceType = BalanceType.POINTS, CurrentBalance = 0 };
        _context.CustomerWallets.Add(wallet);
        return wallet;
    }

    private void Audit(string reviewer, string action, Invoice invoice, string details) =>
        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = reviewer,
            Action = action,
            EntityName = "Invoice",
            EntityId = invoice.InvoiceId.ToString(),
            Details = details,
            CreatedAt = DateTime.UtcNow,
        });

    private async Task InTransactionAsync(Func<Task> work)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            await work();
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }
}

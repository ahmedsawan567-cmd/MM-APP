using Microsoft.EntityFrameworkCore;
using MMApplication.Application.Common;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Services;

/// <summary>
/// إعادة القراءة الآلية للفواتير التي فشلت قراءتها فشلاً مؤقتاً (انتهاء حصة Gemini المجانية أو انقطاع الخدمة).
/// عند نجاح القراءة وعدم وجود سبب للمراجعة تُعتمد الفاتورة وتُضاف النقاط تلقائياً؛ وإلا تبقى للمراجعة ببياناتها المقروءة.
/// </summary>
public class AiRetryService : BackgroundService
{
    public const string ReviewerName = "القراءة الآلية (إعادة تلقائية)";

    /// <summary>فترات الانتظار بين المحاولات (تغطي يوماً كاملاً حتى تتجدد الحصة اليومية).</summary>
    private static readonly TimeSpan[] Schedule =
    [
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1),
        TimeSpan.FromHours(2), TimeSpan.FromHours(4), TimeSpan.FromHours(8), TimeSpan.FromHours(12),
    ];

    public static int MaxAttempts => Schedule.Length;

    /// <summary>موعد المحاولة التالية بعد عدد محاولات سابقة (null = توقف).</summary>
    public static DateTime? NextAttempt(int attemptsSoFar, DateTime now) =>
        attemptsSoFar < Schedule.Length ? now + Schedule[attemptsSoFar] : null;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AiRetryService> _logger;

    public AiRetryService(IServiceScopeFactory scopes, ILogger<AiRetryService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ContinueWith(_ => { });
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await MMApplication.Api.Controllers.StoreClaimsService.ExpireOldAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
                var n = await ProcessDueAsync(scope.ServiceProvider, stoppingToken);
                if (n > 0) _logger.LogInformation("أُعيدت القراءة الآلية لـ {Count} فاتورة.", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذّرت دورة إعادة القراءة الآلية.");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ContinueWith(_ => { });
        }
    }

    /// <summary>يعالج الفواتير المستحقة لإعادة القراءة. يرجع عدد الفواتير التي عولجت.</summary>
    public static async Task<int> ProcessDueAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var ocr = sp.GetService<IReceiptOcrService>();
        if (ocr == null) return 0;
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var config = sp.GetRequiredService<IConfiguration>();
        var review = sp.GetRequiredService<InvoiceReviewService>();
        var logger = sp.GetRequiredService<ILogger<AiRetryService>>();

        var now = DateTime.UtcNow;
        var due = await db.Invoices
            .Where(i => i.NextAiRetryAt != null && i.NextAiRetryAt <= now && i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED)
            .OrderBy(i => i.NextAiRetryAt)
            .Take(10)
            .ToListAsync(ct);

        foreach (var invoice in due)
        {
            var path = ResolveUploadPath(env.WebRootPath, invoice.ReceiptImageUrl);
            if (path == null || !File.Exists(path))
            {
                invoice.NextAiRetryAt = null;
                await db.SaveChangesAsync(ct);
                continue;
            }

            ExtractedReceiptDto result;
            await using (var stream = File.OpenRead(path))
            {
                result = await ocr.ExtractReceiptDataAsync(stream, await ReceiptContextBuilder.BuildAsync(db));
            }
            invoice.AiRetryCount++;

            if (result.RequiresManualReview)
            {
                // ما زال الفشل مؤقتاً: نجرب لاحقاً حسب الجدول؛ وإلا تبقى للمراجعة اليدوية
                invoice.NextAiRetryAt = result.Retryable ? NextAttempt(invoice.AiRetryCount, DateTime.UtcNow) : null;
                var previous = ReceiptReviewPayload.TryParse(invoice.RawAiPayload)?.ReviewReasons ?? new List<string>();
                invoice.RawAiPayload = ReceiptReviewPayload.Serialize(result, previous);
                await db.SaveChangesAsync(ct);
                continue;
            }

            invoice.NextAiRetryAt = null;
            invoice.ExtractedMerchant = result.MerchantName ?? invoice.ExtractedMerchant;
            invoice.InvoiceNumber = result.InvoiceNumber ?? invoice.InvoiceNumber;
            invoice.InvoiceDate = result.InvoiceDate ?? invoice.InvoiceDate;
            invoice.TotalAmount = result.TotalAmount ?? invoice.TotalAmount;
            invoice.TenantId = result.MatchedTenantId ?? invoice.TenantId;
            invoice.ConfidenceScore = (decimal)result.Confidence;

            var reasons = new List<string>();
            if (!result.IsReceipt) reasons.Add("الصورة لا تبدو فاتورة شراء");
            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber) || invoice.TotalAmount is not > 0)
                reasons.Add("تعذّر استخراج رقم الفاتورة أو المبلغ");
            var duplicate = await InvoiceKeys.FindDuplicateAsync(db, invoice.TenantId, InvoiceNumbers.Key(invoice.InvoiceNumber),
                invoice.TotalAmount, invoice.InvoiceDate, invoice.InvoiceId, invoice.ExtractedMerchant);
            if (duplicate != null) reasons.Add($"رقم الفاتورة مستخدم في فاتورة أخرى لنفس المتجر (العميل {duplicate.CustomerId})");
            if (reasons.Count == 0)
            {
                reasons = await InvoiceReviewRules.CollectReasonsAsync(db, config, env, result, invoice.TenantId,
                    invoice.InvoiceDate ?? invoice.CreatedAt, invoice.TotalAmount ?? 0, invoice.FraudRiskScore);
            }

            invoice.RawAiPayload = ReceiptReviewPayload.Serialize(result, reasons);
            db.AuditLogs.Add(new AuditLog
            {
                PerformedBy = ReviewerName,
                Action = "INVOICE_AI_REREAD",
                EntityName = "Invoice",
                EntityId = invoice.InvoiceId.ToString(),
                Details = $"إعادة قراءة تلقائية بـ {result.Provider} (المحاولة {invoice.AiRetryCount}، ثقة {result.Confidence:P0})",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);

            if (reasons.Count == 0)
            {
                var outcome = await review.ApproveAsync(invoice.InvoiceId, ReviewerName);
                if (!outcome.Success) logger.LogWarning("تعذّر الاعتماد التلقائي للفاتورة {Id}: {Message}", invoice.InvoiceId, outcome.Message);
            }
        }
        return due.Count;
    }

    public static string? ResolveUploadPath(string? webRoot, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || webRoot == null || url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
        var full = Path.GetFullPath(Path.Combine(webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(Path.GetFullPath(webRoot), StringComparison.Ordinal) ? full : null;
    }
}

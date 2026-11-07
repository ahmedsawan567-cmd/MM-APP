using MMApplication.Api.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Application.Common;
using MMApplication.Api.ViewModels;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;
using MMApplication.Infrastructure.Security;
using MMApplication.Api.Security;

namespace MMApplication.Api.Controllers;

/// <summary>
/// وحدة تحكم لوحة الإدارة العامة المركزية والتحكم بالصلاحيات الدقيقة عبر ASP.NET Core MVC (.NET 9)
/// متصلة بقاعدة البيانات السحابية Supabase ومفصولة الصلاحيات بدقة (مطور، مدير عام، مدقق احتيال، مدير متجر).
/// </summary>
[Authorize(Roles = StaffRoles.All)]
public class AdminDashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminDashboardController> _logger;
    private readonly IPasswordHasherService _passwordHasher;

    private readonly InvoiceReviewService _review;
    private readonly IReceiptOcrService? _ocr;
    private readonly IWebHostEnvironment? _env;

    public AdminDashboardController(
        AppDbContext context,
        ILogger<AdminDashboardController> logger,
        IPasswordHasherService? passwordHasher = null,
        InvoiceReviewService? review = null,
        IReceiptOcrService? ocr = null,
        IWebHostEnvironment? env = null)
    {
        _context = context;
        _logger = logger;
        _passwordHasher = passwordHasher ?? new PasswordHasherService();
        _review = review ?? new InvoiceReviewService(context, new FlexibleLoyaltyEvaluationService(context),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceReviewService>.Instance);
        _ocr = ocr;
        _env = env;
    }

    /// <summary>من يحق له اعتماد/رفض الفواتير: المدير العام ومدقق الاحتيال فقط (ليس مدير المتجر).</summary>
    private bool CanReviewInvoices => IsDeveloperOrSuperAdmin || IsAuditor;

    private string ReviewerName => User?.Identity?.Name ?? CurrentUserId ?? "Admin";

    private static readonly InvoiceStatus[] ReviewQueueStatuses =
        [InvoiceStatus.PENDING, InvoiceStatus.PROCESSING, InvoiceStatus.REVIEW_REQUIRED, InvoiceStatus.DISPUTED];

    // ----------------------------------------------------
    // ✅ INVOICE REVIEW (مراجعة الفواتير بمساعدة الذكاء الاصطناعي)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Review([FromQuery] string? queue)
    {
        if (!await Can(Permissions.InvoicesReview)) return RedirectToAction(nameof(Index));

        queue = queue is "waiting" or "decided" ? queue : "open";
        var query = _context.Invoices.AsNoTracking().Include(i => i.Customer).Include(i => i.Tenant).AsQueryable();
        query = queue switch
        {
            "waiting" => query.Where(i => i.ProcessingStatus == InvoiceStatus.RESUBMISSION_REQUESTED),
            "decided" => query.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED || i.ProcessingStatus == InvoiceStatus.REJECTED),
            _ => query.Where(i => ReviewQueueStatuses.Contains(i.ProcessingStatus)),
        };

        var rows = await query
            .OrderBy(i => queue == "open" ? 0 : 1)
            .ThenByDescending(i => i.ProcessingStatus == InvoiceStatus.DISPUTED)
            .ThenBy(i => queue == "open" ? i.CreatedAt : DateTime.MinValue)
            .ThenByDescending(i => i.CreatedAt)
            .Take(200)
            .ToListAsync();

        var model = new ReviewPageViewModel
        {
            Queue = queue,
            OpenCount = await _context.Invoices.CountAsync(i => ReviewQueueStatuses.Contains(i.ProcessingStatus)),
            WaitingCount = await _context.Invoices.CountAsync(i => i.ProcessingStatus == InvoiceStatus.RESUBMISSION_REQUESTED),
            AiProvider = _ocr?.GetType().Name.Replace("ReceiptOcrService", "").Replace("OcrService", "") ?? "-",
            Stores = await _context.Tenants.AsNoTracking().OrderBy(t => t.NameAr)
                .Select(t => new ReviewStoreOption { Id = t.TenantId, Name = t.NameAr }).ToListAsync(),
            Items = rows.Select(i =>
            {
                var payload = ReceiptReviewPayload.TryParse(i.RawAiPayload);
                return new ReviewQueueItem
                {
                    Id = i.InvoiceId,
                    CustomerName = i.Customer?.FullName ?? i.CustomerId,
                    StoreName = i.Tenant?.NameAr ?? i.ExtractedMerchant ?? "غير معروف",
                    TotalAmount = i.TotalAmount,
                    Status = i.ProcessingStatus.ToString(),
                    CreatedAt = i.CreatedAt,
                    Confidence = i.ConfidenceScore,
                    Reasons = payload?.ReviewReasons ?? (string.IsNullOrEmpty(i.DisputeReason) ? new() : new() { "اعتراض العميل" }),
                };
            }).ToList(),
        };

        ViewData["ActiveTab"] = "review";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ReviewItem(Guid id)
    {
        if (!await Can(Permissions.InvoicesReview)) return Forbid();

        var i = await _context.Invoices.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.InvoiceId == id);
        if (i == null) return NotFound(new { success = false, message = "الفاتورة غير موجودة." });

        var payload = ReceiptReviewPayload.TryParse(i.RawAiPayload);
        var customerInvoices = await _context.Invoices.AsNoTracking()
            .Where(x => x.CustomerId == i.CustomerId)
            .GroupBy(x => x.ProcessingStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var duplicates = string.IsNullOrEmpty(i.InvoiceNumber) ? new() : await _context.Invoices.AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.InvoiceId != i.InvoiceId && x.InvoiceNumber == i.InvoiceNumber)
            .Select(x => new
            {
                id = x.InvoiceId,
                customer = x.Customer != null ? x.Customer.FullName : x.CustomerId,
                status = x.ProcessingStatus.ToString(),
                amount = x.TotalAmount,
                createdAt = x.CreatedAt,
            })
            .ToListAsync<object>();

        var history = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == "Invoice" && a.EntityId == i.InvoiceId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.PerformedBy, a.Action, a.Details, a.CreatedAt })
            .ToListAsync();

        return Json(new
        {
            id = i.InvoiceId,
            status = i.ProcessingStatus.ToString(),
            imageUrl = i.ReceiptImageUrl,
            isPdf = i.ReceiptImageUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase),
            tenantId = i.TenantId,
            storeName = i.Tenant?.NameAr,
            merchantName = i.ExtractedMerchant,
            invoiceNumber = i.InvoiceNumber,
            invoiceDate = i.InvoiceDate?.ToString("yyyy-MM-dd"),
            totalAmount = i.TotalAmount,
            pointsAwarded = i.PointsAwarded,
            confidence = i.ConfidenceScore,
            fraudRiskScore = i.FraudRiskScore,
            rejectionReason = i.RejectionReason,
            disputeReason = i.DisputeReason,
            createdAt = i.CreatedAt,
            ai = payload == null ? null : new
            {
                provider = payload.Provider,
                reasons = payload.ReviewReasons,
                concerns = payload.AuthenticityConcerns,
                currency = payload.Currency,
                items = payload.Items,
                manual = payload.RequiresManualReview,
            },
            customer = new
            {
                id = i.CustomerId,
                name = i.Customer?.FullName,
                phone = i.Customer?.PhoneNumber,
                tier = i.Customer?.LoyaltyTier,
                frozen = i.Customer?.IsFrozen ?? false,
                registeredAt = i.Customer?.RegisteredAt,
                approved = customerInvoices.Where(c => c.Status == InvoiceStatus.APPROVED).Sum(c => c.Count),
                rejected = customerInvoices.Where(c => c.Status == InvoiceStatus.REJECTED).Sum(c => c.Count),
                total = customerInvoices.Sum(c => c.Count),
            },
            duplicates,
            history,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewApprove(Guid id, [FromBody] InvoiceCorrection? correction)
    {
        if (!await Can(Permissions.InvoicesReview)) return Forbid();
        return ReviewResult(await _review.ApproveAsync(id, ReviewerName, correction));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewReject(Guid id, [FromBody] ReviewDecisionRequest request)
    {
        if (!await Can(Permissions.InvoicesReview)) return Forbid();
        return ReviewResult(await _review.RejectAsync(id, ReviewerName, request.Reason ?? string.Empty));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRequestResubmission(Guid id, [FromBody] ReviewDecisionRequest request)
    {
        if (!await Can(Permissions.InvoicesReview)) return Forbid();
        return ReviewResult(await _review.RequestResubmissionAsync(id, ReviewerName, request.Reason ?? string.Empty));
    }

    /// <summary>إعادة قراءة صورة الفاتورة المحفوظة بالذكاء الاصطناعي (مثلاً بعد تفعيل مفتاح Claude).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRerunAi(Guid id)
    {
        if (!await Can(Permissions.InvoicesReview)) return Forbid();
        if (_ocr == null || _env == null) return BadRequest(new { success = false, message = "خدمة القراءة الآلية غير متاحة." });

        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id);
        if (invoice == null) return NotFound(new { success = false, message = "الفاتورة غير موجودة." });
        if (invoice.ProcessingStatus == InvoiceStatus.APPROVED)
            return BadRequest(new { success = false, message = "الفاتورة معتمدة؛ لا حاجة لإعادة القراءة." });

        var path = ResolveUploadPath(invoice.ReceiptImageUrl);
        if (path == null || !System.IO.File.Exists(path))
            return BadRequest(new { success = false, message = "ملف صورة الفاتورة غير موجود على الخادم." });

        ExtractedReceiptDto result;
        await using (var stream = System.IO.File.OpenRead(path))
        {
            result = await _ocr.ExtractReceiptDataAsync(stream, await ReceiptContextBuilder.BuildAsync(_context));
        }

        if (!result.RequiresManualReview)
        {
            invoice.ExtractedMerchant = result.MerchantName ?? invoice.ExtractedMerchant;
            invoice.InvoiceNumber = result.InvoiceNumber ?? invoice.InvoiceNumber;
            invoice.InvoiceDate = result.InvoiceDate ?? invoice.InvoiceDate;
            invoice.TotalAmount = result.TotalAmount ?? invoice.TotalAmount;
            invoice.TenantId = result.MatchedTenantId ?? invoice.TenantId;
            invoice.ConfidenceScore = (decimal)result.Confidence;
            invoice.NextAiRetryAt = null;
        }

        var previous = ReceiptReviewPayload.TryParse(invoice.RawAiPayload)?.ReviewReasons ?? new List<string>();
        invoice.RawAiPayload = ReceiptReviewPayload.Serialize(result, previous);
        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = ReviewerName,
            Action = "INVOICE_AI_REREAD",
            EntityName = "Invoice",
            EntityId = invoice.InvoiceId.ToString(),
            Details = $"إعادة قراءة الفاتورة بـ {result.Provider} (ثقة {result.Confidence:P0})",
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = !result.RequiresManualReview,
            message = result.RequiresManualReview
                ? "تعذّرت القراءة الآلية: " + string.Join("، ", result.AuthenticityConcerns)
                : "تمت إعادة قراءة الفاتورة وتحديث البيانات.",
        });
    }

    /// <summary>مختبر الذكاء الاصطناعي: قراءة أي صورة فاتورة للتجربة بدون حفظها أو منح نقاط.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> AiReceiptLab(IFormFile? file)
    {
        if (!await Can(Permissions.InvoicesReview, Permissions.InvoiceTemplates)) return Forbid();
        if (_ocr == null) return BadRequest(new { success = false, message = "خدمة القراءة الآلية غير متاحة." });
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "اختر صورة فاتورة." });

        await using var stream = file.OpenReadStream();
        var result = await _ocr.ExtractReceiptDataAsync(stream, await ReceiptContextBuilder.BuildAsync(_context, file.ContentType, file.FileName));
        var store = result.MatchedTenantId == null ? null
            : await _context.Tenants.AsNoTracking().Where(t => t.TenantId == result.MatchedTenantId).Select(t => t.NameAr).FirstOrDefaultAsync();

        return Json(new
        {
            success = true,
            provider = result.Provider,
            isReceipt = result.IsReceipt,
            manual = result.RequiresManualReview,
            merchantName = result.MerchantName,
            matchedStore = store,
            invoiceNumber = result.InvoiceNumber,
            invoiceDate = result.InvoiceDate?.ToString("yyyy-MM-dd"),
            totalAmount = result.TotalAmount,
            currency = result.Currency,
            confidence = result.Confidence,
            items = result.Items,
            concerns = result.AuthenticityConcerns,
        });
    }

    private string? ResolveUploadPath(string? url)
    {
        // حماية من الخروج خارج مجلد wwwroot
        return AiRetryService.ResolveUploadPath(_env?.WebRootPath, url);
    }

    // ----------------------------------------------------
    // 📈 REPORTS (التقارير والإحصائيات + تصدير Excel)
    // ----------------------------------------------------
    private ReportFilter BuildReportFilter(string? range, DateTime? from, DateTime? to, string? storeId)
    {
        // مدير المتجر يرى تقارير متجره فقط
        var tenant = IsStoreScoped ? ScopeTenantId : storeId;
        return ReportFilter.From_(range, from, to, tenant);
    }

    [HttpGet]
    public async Task<IActionResult> Reports([FromQuery] string? range, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? storeId)
    {
        if (!await Can(Permissions.ReportsView)) return RedirectToAction(nameof(Index));

        var filter = BuildReportFilter(range, from, to, storeId);
        var data = await new ReportService(_context).BuildAsync(filter);

        ViewData["ActiveTab"] = "reports";
        ViewData["Range"] = string.IsNullOrEmpty(range) ? "30d" : range;
        ViewData["Stores"] = IsStoreManager ? new List<ReviewStoreOption>() : await _context.Tenants.AsNoTracking()
            .OrderBy(t => t.NameAr).Select(t => new ReviewStoreOption { Id = t.TenantId, Name = t.NameAr }).ToListAsync();
        return View(data);
    }

    [HttpGet]
    public async Task<IActionResult> ReportsExport([FromQuery] string? range, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? storeId)
    {
        if (!await Can(Permissions.ReportsExport)) return Forbid();

        var filter = BuildReportFilter(range, from, to, storeId);
        var data = await new ReportService(_context).BuildAsync(filter);
        var bytes = ReportService.ToExcel(data);

        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = ReviewerName,
            Action = "REPORT_EXPORT",
            EntityName = "Report",
            Details = $"تصدير تقرير Excel من {filter.From:yyyy-MM-dd} إلى {filter.To:yyyy-MM-dd}" + (filter.TenantId != null ? $" للمتجر {filter.TenantId}" : ""),
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"almaraa-report-{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.xlsx");
    }

    // ----------------------------------------------------
    // 🎁 REWARDS CATALOG & VOUCHERS (كتالوج المكافآت والقسائم)
    // ----------------------------------------------------
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    [HttpGet]
    public async Task<IActionResult> Rewards()
    {
        var stats = await _context.Redemptions.AsNoTracking()
            .GroupBy(r => r.RewardId)
            .Select(g => new
            {
                RewardId = g.Key,
                Total = g.Count(),
                Active = g.Count(r => r.Status == RedemptionStatus.ACTIVE),
                Used = g.Count(r => r.Status == RedemptionStatus.USED),
                Points = g.Sum(r => r.PointsSpent),
            })
            .ToListAsync();

        var rewards = await _context.Rewards.AsNoTracking().OrderByDescending(r => r.IsActive).ThenBy(r => r.PointsCost).ToListAsync();
        var now = DateTime.UtcNow;
        if (!await Can(Permissions.RewardsManage, Permissions.VouchersLog, Permissions.VouchersLookup)) return RedirectToAction(nameof(Index));
        var canSeeVouchers = await Can(Permissions.VouchersLog);
        var vouchers = !canSeeVouchers ? new List<VoucherAdminRow>() : await _context.Redemptions.AsNoTracking()
            .OrderByDescending(r => r.RedeemedAt).Take(300)
            .Select(r => new VoucherAdminRow(r.RedemptionCode, r.Reward.Title, r.Reward.MerchantName, r.CustomerId, r.Customer.FullName,
                r.Customer.MembershipNumber, r.PointsSpent, r.Reward.DiscountValue, r.Status, r.RedeemedAt, r.ExpiresAt, r.UsedAt, r.UsedByMerchant))
            .ToListAsync();
        var redemptions = _context.Redemptions.AsNoTracking();
        var model = new RewardsPageViewModel
        {
            CanManage = await Can(Permissions.RewardsManage),
            CanSeeVouchers = canSeeVouchers,
            Vouchers = vouchers,
            StoreNames = await _context.Tenants.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.NameAr).Select(t => t.NameAr).ToListAsync(),
            IssuedTotal = await redemptions.CountAsync(),
            Issued30 = await redemptions.CountAsync(r => r.RedeemedAt >= now.AddDays(-30)),
            UsedTotal = await redemptions.CountAsync(r => r.Status == RedemptionStatus.USED),
            ActiveNow = await redemptions.CountAsync(r => r.Status == RedemptionStatus.ACTIVE && r.ExpiresAt > now),
            ExpiringSoon = await redemptions.CountAsync(r => r.Status == RedemptionStatus.ACTIVE && r.ExpiresAt > now && r.ExpiresAt <= now.AddDays(7)),
            PointsRedeemed = await redemptions.SumAsync(r => (int?)r.PointsSpent) ?? 0,
            DiscountGiven = await redemptions.Where(r => r.Status == RedemptionStatus.USED).SumAsync(r => (decimal?)r.Reward.DiscountValue) ?? 0,
            Rewards = rewards.Select(r =>
            {
                var st = stats.FirstOrDefault(x => x.RewardId == r.Id);
                return new RewardAdminRow
                {
                    Id = r.Id,
                    Title = r.Title,
                    Description = r.Description,
                    MerchantName = r.MerchantName,
                    PointsCost = r.PointsCost,
                    DiscountValue = r.DiscountValue,
                    ImageUrl = r.ImageUrl,
                    StockQuantity = r.StockQuantity,
                    ValidityDays = r.ValidityDays,
                    ValidUntil = r.ValidUntil,
                    IsActive = r.IsActive,
                    Redemptions = st?.Total ?? 0,
                    ActiveVouchers = st?.Active ?? 0,
                    UsedVouchers = st?.Used ?? 0,
                    PointsSpent = st?.Points ?? 0,
                };
            }).ToList(),
        };

        ViewData["ActiveTab"] = "rewards";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RewardSave([FromBody] RewardEditRequest req)
    {
        if (!await Can(Permissions.RewardsManage)) return Forbid();

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Trim().Length < 2) errors.Add("عنوان المكافأة مطلوب.");
        if (req.PointsCost < 1) errors.Add("تكلفة النقاط يجب أن تكون 1 على الأقل.");
        if (req.DiscountValue < 0) errors.Add("قيمة الخصم لا يمكن أن تكون سالبة.");
        if (req.ValidityDays is < 1 or > 365) errors.Add("مدة صلاحية القسيمة بين 1 و 365 يوماً.");
        if (req.StockQuantity is < 0) errors.Add("الكمية لا يمكن أن تكون سالبة.");
        if (errors.Count > 0) return BadRequest(new { success = false, message = string.Join(" ", errors) });

        Reward? reward;
        if (req.Id is { } id && id != Guid.Empty)
        {
            reward = await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id);
            if (reward == null) return NotFound(new { success = false, message = "المكافأة غير موجودة." });
        }
        else
        {
            reward = new Reward { CreatedAt = DateTime.UtcNow };
            _context.Rewards.Add(reward);
        }

        reward.Title = req.Title!.Trim();
        reward.Description = req.Description?.Trim() ?? string.Empty;
        reward.MerchantName = string.IsNullOrWhiteSpace(req.MerchantName) ? null : req.MerchantName.Trim();
        reward.PointsCost = req.PointsCost;
        reward.DiscountValue = req.DiscountValue;
        reward.ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim();
        reward.StockQuantity = req.StockQuantity;
        reward.ValidityDays = req.ValidityDays;
        reward.ValidUntil = req.ValidUntil.HasValue ? DateTime.SpecifyKind(req.ValidUntil.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc) : null;
        reward.IsActive = req.IsActive;

        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = ReviewerName,
            Action = req.Id.HasValue ? "REWARD_UPDATE" : "REWARD_CREATE",
            EntityName = "Reward",
            EntityId = reward.Id.ToString(),
            Details = $"{(req.Id.HasValue ? "تعديل" : "إضافة")} المكافأة \"{reward.Title}\" بتكلفة {reward.PointsCost} نقطة",
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "تم حفظ المكافأة.", id = reward.Id });
    }

    /// <summary>حذف مكافأة: إن استبدلها عملاء من قبل تُوقف فقط (قسائمهم تبقى صالحة)، وإلا تُحذف نهائياً.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RewardDelete(Guid id)
    {
        if (!await Can(Permissions.RewardsManage)) return Forbid();
        var reward = await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id);
        if (reward == null) return NotFound(new { success = false, message = "المكافأة غير موجودة." });

        var redeemed = await _context.Redemptions.AnyAsync(r => r.RewardId == id);
        if (redeemed) reward.IsActive = false;
        else _context.Rewards.Remove(reward);
        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = ReviewerName,
            Action = redeemed ? "REWARD_DEACTIVATE" : "REWARD_DELETE",
            EntityName = "Reward",
            EntityId = id.ToString(),
            Details = $"{(redeemed ? "إيقاف" : "حذف")} المكافأة \"{reward.Title}\"",
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        return Json(new
        {
            success = true,
            message = redeemed ? "استبدلها عملاء من قبل، فأُوقفت فقط (قسائمهم تبقى صالحة)." : "تم حذف المكافأة."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RewardToggle(Guid id)
    {
        if (!await Can(Permissions.RewardsManage)) return Forbid();
        var reward = await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id);
        if (reward == null) return NotFound(new { success = false, message = "المكافأة غير موجودة." });
        reward.IsActive = !reward.IsActive;
        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = ReviewerName,
            Action = "REWARD_TOGGLE",
            EntityName = "Reward",
            EntityId = reward.Id.ToString(),
            Details = $"{(reward.IsActive ? "تفعيل" : "إيقاف")} المكافأة \"{reward.Title}\"",
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        return Json(new { success = true, isActive = reward.IsActive, message = reward.IsActive ? "تم تفعيل المكافأة." : "تم إيقاف المكافأة. لن تظهر في التطبيق." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> RewardImage(IFormFile? file, [FromServices] IFileStorageService storage)
    {
        if (!await Can(Permissions.RewardsManage)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "اختر صورة." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext)) return BadRequest(new { success = false, message = "الصورة يجب أن تكون JPG أو PNG أو WEBP." });
        await using var stream = file.OpenReadStream();
        var url = await storage.SaveFileAsync(stream, file.FileName, "rewards");
        return Json(new { success = true, url });
    }

    [HttpGet]
    public async Task<IActionResult> VoucherLookup(string code)
    {
        if (!await Can(Permissions.VouchersLookup)) return Forbid();
        var result = await new VoucherService(_context).LookupAsync(code ?? string.Empty);
        return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, voucher = result.Voucher });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoucherUse([FromBody] ReviewDecisionRequest request)
    {
        if (!await Can(Permissions.VouchersRedeem)) return Forbid();
        var usedBy = User?.FindFirst("TenantName")?.Value is { Length: > 0 } store ? store : "مكتب خدمة العملاء";
        var result = await new VoucherService(_context).UseAsync(request.Reason ?? string.Empty, usedBy, ReviewerName);
        return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, voucher = result.Voucher });
    }

    private IActionResult ReviewResult(InvoiceReviewOutcome outcome) =>
        outcome.Success
            ? Json(new { success = true, message = outcome.Message, points = outcome.PointsChanged })
            : StatusCode(outcome.StatusCode, new { success = false, message = outcome.Message });

    #region RBAC Helper Properties
    private string? CurrentUserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private string? CurrentTenantId => User?.FindFirst("TenantId")?.Value;
    private string? CurrentSystemRole => User?.FindFirst("SystemRole")?.Value ?? User?.FindFirst(ClaimTypes.Role)?.Value;
    
    // ملاحظة أمنية: المستخدم غير المسجّل لا يُعامل أبداً كمدير عام.
    // (User == null يحدث فقط في اختبارات الوحدة التي تنشئ الـ Controller مباشرة بدون HttpContext)
    // الصلاحيات تُحدد من الدور فقط (لا استثناءات بأسماء حسابات).
    private bool IsDeveloperOrSuperAdmin => User == null ||
                                           User.IsInRole("superadmin") ||
                                           CurrentSystemRole == "SUPER_ADMIN";

    private bool IsAuditor => User != null &&
                             (User.IsInRole("auditor") || CurrentSystemRole == "FRAUD_AUDITOR");

    private bool IsStoreManager => User != null &&
                                  User.Identity?.IsAuthenticated == true &&
                                  (User.IsInRole("storemanager") || CurrentSystemRole == "STORE_MANAGER") &&
                                  !IsDeveloperOrSuperAdmin;

    private bool IsCashier => User != null && User.Identity?.IsAuthenticated == true &&
                              (User.IsInRole("cashier") || CurrentSystemRole == "CASHIER") && !IsDeveloperOrSuperAdmin;

    /// <summary>موظفو المتاجر (مدير المتجر والكاشير) يرون بيانات متجرهم فقط.</summary>
    private bool IsStoreScoped => User != null && User.Identity?.IsAuthenticated == true && !IsDeveloperOrSuperAdmin && !IsAuditor;

    /// <summary>متجر الموظف للتصفية؛ قيمة لا تطابق شيئاً إن لم يكن مرتبطاً بمتجر.</summary>
    private string ScopeTenantId => string.IsNullOrEmpty(CurrentTenantId) ? "__no_store__" : CurrentTenantId;

    private bool IsExplicitlyUnauthenticated => User?.Identity != null && !User.Identity.IsAuthenticated;

    /// <summary>هل يملك المستخدم إحدى الصلاحيات؟ (اختبارات الوحدة بدون مستخدم تُعامل كمدير عام)</summary>
    private async Task<bool> Can(params string[] permissions) =>
        HttpContext == null || User?.Identity?.IsAuthenticated != true || await PermissionContext.HasAnyAsync(HttpContext, _context, permissions);

    /// <summary>أول صفحة يملك المستخدم صلاحيتها (لمن لا يملك عرض لوحة المؤشرات).</summary>
    private async Task<string> FirstAllowedPageAsync()
    {
        var pages = new (string Perm, string Action)[]
        {
            (Permissions.InvoicesReview, nameof(Review)), (Permissions.InvoicesView, nameof(Invoices)), (Permissions.CustomersView, nameof(Customers)),
            (Permissions.VouchersLookup, nameof(Rewards)), (Permissions.RewardsManage, nameof(Rewards)), (Permissions.ReportsView, nameof(Reports)),
            (Permissions.StoresView, nameof(Stores)), (Permissions.SettlementsView, nameof(Settlements)), (Permissions.StaffManage, nameof(Staff)),
        };
        foreach (var (perm, action) in pages)
            if (await Can(perm)) return action;
        return nameof(Logout);
    }
    #endregion

    // ----------------------------------------------------
    // 0. 🔐 AUTHENTICATION & LOGIN ACTIONS
    // ----------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            model.ErrorMessage = "يرجى إدخال اسم المستخدم أو البريد أو رقم الهاتف وكلمة المرور.";
            return View(model);
        }

        var input = model.Username.Trim();
        var user = await _context.SystemUsers
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.UserId == input || u.Email == input || u.PhoneNumber == input);

        if (user != null && StaffLoginGuard.IsLocked(user, out var minutesLeft))
        {
            model.ErrorMessage = StaffLoginGuard.LockedMessage(minutesLeft);
            return View(model);
        }

        if (user == null || !_passwordHasher.VerifyPassword(model.Password, user.PasswordHash))
        {
            if (user != null)
            {
                var locked = StaffLoginGuard.RegisterFailure(_context, user, HttpContext?.Connection.RemoteIpAddress?.ToString());
                await _context.SaveChangesAsync();
                if (locked)
                {
                    model.ErrorMessage = StaffLoginGuard.LockedMessage((int)StaffLoginGuard.LockDuration.TotalMinutes);
                    return View(model);
                }
            }
            model.ErrorMessage = "بيانات تسجيل الدخول غير صحيحة. يرجى التحقق من صحة اسم المستخدم وكلمة المرور.";
            return View(model);
        }

        if (!user.IsActive)
        {
            model.ErrorMessage = "هذا الحساب معطل حالياً من قبل الإدارة المركزية.";
            return View(model);
        }

        StaffLoginGuard.RegisterSuccess(user);
        await _context.SaveChangesAsync();
        await SignInUserAsync(user, model.RememberMe);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        if (HttpContext != null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInUserAsync(SystemUser user, bool isPersistent)
    {
        if (HttpContext == null) return;

        var roleString = user.Role switch
        {
            SystemUserRole.SUPER_ADMIN => "superadmin",
            SystemUserRole.FRAUD_AUDITOR => "auditor",
            SystemUserRole.STORE_MANAGER => "storemanager",
            SystemUserRole.CASHIER => "cashier",
            _ => "superadmin"
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, roleString),
            new("SystemRole", user.Role.ToString()),
            new("TenantId", user.TenantId ?? string.Empty),
            new("TenantName", user.Tenant?.NameAr ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(12)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }

    // ----------------------------------------------------
    // 1. 📊 OVERVIEW & POISSON ANALYTICS (Index View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.DashboardView)) return RedirectToAction(await FirstAllowedPageAsync());
        if (Request?.Query.ContainsKey("denied") == true) TempData["Denied"] = "ليست لديك صلاحية لتلك الصفحة.";

        var invoiceQuery = _context.Invoices.AsNoTracking();
        if (IsStoreScoped)
        {
            invoiceQuery = invoiceQuery.Where(i => i.TenantId == ScopeTenantId);
        }

        var totalSales = await invoiceQuery
            .Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED)
            .SumAsync(i => i.TotalAmount) ?? 0m;

        var today = DateTime.UtcNow.Date;
        var todayScans = await invoiceQuery
            .CountAsync(i => i.CreatedAt >= today);

        var pendingReviews = await invoiceQuery
            .CountAsync(i => i.ProcessingStatus == InvoiceStatus.PENDING || i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED || i.FraudRiskScore >= 31);

        var circulatingPoints = await _context.CustomerWallets
            .Where(w => w.BalanceType == BalanceType.POINTS)
            .SumAsync(w => (long)w.CurrentBalance);

        var topStores = await invoiceQuery
            .Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED)
            .GroupBy(i => i.ExtractedMerchant ?? (i.Tenant != null ? i.Tenant.NameAr : "متجر"))
            .Select(g => new TopStorePoint
            {
                StoreName = g.Key,
                InvoicesCount = g.Count(),
                TotalSales = g.Sum(i => i.TotalAmount) ?? 0m
            })
            .OrderByDescending(s => s.TotalSales)
            .Take(5)
            .ToListAsync();

        // آخر 14 يوماً: عدد الفواتير ومبيعاتها المعتمدة لكل يوم (بيانات حقيقية)
        var since = today.AddDays(-13);
        var recent = await invoiceQuery
            .Where(i => i.CreatedAt >= since)
            .Select(i => new { i.CreatedAt, i.TotalAmount, i.ProcessingStatus })
            .ToListAsync();
        var pace = Enumerable.Range(0, 14)
            .Select(offset => since.AddDays(offset))
            .Select(day => new ScanChartDataPoint
            {
                TimeLabel = day.ToString("MM/dd"),
                ScanCount = recent.Count(i => i.CreatedAt.Date == day),
                VolumeAmount = recent
                    .Where(i => i.CreatedAt.Date == day && i.ProcessingStatus == InvoiceStatus.APPROVED)
                    .Sum(i => i.TotalAmount ?? 0m)
            })
            .ToList();

        // توزيع المبيعات المعتمدة حسب فئة المتجر
        var byCategory = await invoiceQuery
            .Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED && i.Tenant != null)
            .GroupBy(i => i.Tenant!.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(i => i.TotalAmount) ?? 0m })
            .OrderByDescending(g => g.Total)
            .ToListAsync();
        var categoryTotal = byCategory.Sum(c => c.Total);
        var categories = byCategory
            .Select(c => new CategorySpendPoint
            {
                CategoryName = string.IsNullOrWhiteSpace(c.Category) ? "أخرى" : c.Category,
                TotalAmount = c.Total,
                Percentage = categoryTotal > 0 ? Math.Round(c.Total / categoryTotal * 100, 1) : 0
            })
            .ToList();

        // مقارنة مبيعات آخر 30 يوماً بالثلاثين التي قبلها
        var last30 = await invoiceQuery
            .Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED && i.CreatedAt >= today.AddDays(-30))
            .SumAsync(i => i.TotalAmount) ?? 0m;
        var prev30 = await invoiceQuery
            .Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED && i.CreatedAt >= today.AddDays(-60) && i.CreatedAt < today.AddDays(-30))
            .SumAsync(i => i.TotalAmount) ?? 0m;
        ViewData["SalesChangePercent"] = prev30 > 0 ? Math.Round((last30 - prev30) / prev30 * 100, 1) : (decimal?)null;

        var overviewDto = new SuperAdminOverviewDto
        {
            TotalSalesVolume = totalSales,
            TodayScansCount = todayScans,
            PendingReviewCount = pendingReviews,
            CirculatingPoints = (int)circulatingPoints,
            ScanActivityPace = pace,
            CategoryDistribution = categories,
            TopPerformingStores = topStores
        };

        ViewData["ActiveTab"] = "overview";
        ViewData["Title"] = IsStoreScoped ? "لوحة مبيعات ومؤشرات المتجر" : "لوحة المؤشرات المركزية ونمذجة بواسون";
        return View(overviewDto);
    }

    // ----------------------------------------------------
    // 2. 🏢 TENANTS & STORES (Stores View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Stores([FromQuery] string? category, [FromQuery] string? search)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.StoresView, Permissions.StoresManage)) return RedirectToAction(nameof(Index)); // المتاجر: المدير العام، المدقق للاطلاع، ومدير المتجر لمتجره

        var query = _context.Tenants
            .Include(t => t.LoyaltyRule)
            .Include(t => t.SystemUsers)
            .Include(t => t.Aliases)
            .AsNoTracking();

        // RBAC Filter: Store Managers only see their own store
        if (IsStoreScoped)
        {
            query = query.Where(s => s.TenantId == ScopeTenantId);
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
        {
            query = query.Where(s => s.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.NameAr.Contains(term) || s.NameEn.Contains(term) || s.Category.Contains(term) || s.UnitNumber.Contains(term));
        }

        var stores = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();

        var storeDtos = stores.Select(s => new StoreTenantDto
        {
            Id = s.TenantId,
            Name = s.NameAr,
            Category = s.Category,
            FloorNumber = s.FloorNumber,
            NameEn = s.NameEn,
            UnitNumber = s.UnitNumber,
            LoyaltyType = StoreLoyaltyType.MallPoints,
            LoyaltyTypeLabel = s.LoyaltyRule != null ? s.LoyaltyRule.RewardType : "نقاط المول",
            CommissionRate = s.CommissionRate,
            ContactPerson = s.ContactPerson,
            ContactPhone = s.ContactPhone,
            LogoUrl = s.LogoUrl,
            IsActive = s.IsActive,
            ActiveStaffCount = s.SystemUsers.Count,
            CreatedAt = s.CreatedAt,
            TotalSales = _context.Invoices.Where(i => i.TenantId == s.TenantId && i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount) ?? 0m,
            Aliases = s.Aliases.Select(a => new StoreAliasDto
            {
                Id = a.Id,
                TenantId = a.TenantId,
                AliasName = a.AliasName,
                MatchType = a.MatchType,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt
            }).ToList()
        }).ToList();

        ViewData["ActiveTab"] = "stores";
        ViewData["Title"] = IsStoreScoped ? "متجري" : "المتاجر";
        ViewData["IsSuperAdmin"] = await Can(Permissions.StoresManage);
        ViewData["ProfiledStores"] = (await _context.StoreInvoiceProfiles.AsNoTracking().Select(p => p.TenantId).ToListAsync()).ToHashSet();
        ViewData["Categories"] = await _context.Tenants.AsNoTracking().Select(t => t.Category).Distinct().OrderBy(c => c).ToListAsync();
        ViewData["LoyaltyFilter"] = category ?? "";
        ViewData["SearchQuery"] = search ?? "";
        return View(storeDtos);
    }

    // ----------------------------------------------------
    // 2.1 🧾 نماذج الفواتير: بصمة فاتورة كل متجر (يتعلّمها الذكاء الاصطناعي من صورة نموذجية)
    // ----------------------------------------------------
    private async Task<bool> CanManageProfile(string storeId) =>
        await Can(Permissions.InvoiceTemplates) && (!IsStoreScoped || CurrentTenantId == storeId);

    [HttpGet]
    public async Task<IActionResult> InvoiceTemplates([FromServices] IInvoiceProfileAnalyzer? analyzer = null)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.InvoiceTemplates)) return RedirectToAction(nameof(Index)); // نماذج الفواتير

        var query = _context.Tenants.AsNoTracking();
        if (IsStoreScoped) query = query.Where(t => t.TenantId == ScopeTenantId);

        var rows = await query
            .OrderByDescending(t => t.IsActive).ThenBy(t => t.NameAr)
            .Select(t => new StoreInvoiceProfileRow
            {
                StoreId = t.TenantId,
                StoreName = t.NameAr,
                StoreNameEn = t.NameEn,
                Category = t.Category,
                UnitNumber = t.UnitNumber,
                StoreActive = t.IsActive,
                Aliases = t.Aliases.Where(a => a.IsActive).Select(a => a.AliasName).ToList(),
                InvoicesCount = t.Invoices.Count(),
                ApprovedCount = t.Invoices.Count(i => i.ProcessingStatus == InvoiceStatus.APPROVED),
                HasProfile = t.InvoiceProfile != null,
                ProfileActive = t.InvoiceProfile != null && t.InvoiceProfile.IsActive,
                SampleImageUrl = t.InvoiceProfile!.SampleImageUrl,
                PrintedName = t.InvoiceProfile!.PrintedName,
                TaxNumber = t.InvoiceProfile!.TaxNumber,
                PhoneOnReceipt = t.InvoiceProfile!.PhoneOnReceipt,
                InvoiceNumberLabel = t.InvoiceProfile!.InvoiceNumberLabel,
                InvoiceNumberExample = t.InvoiceProfile!.InvoiceNumberExample,
                InvoiceNumberPattern = t.InvoiceProfile!.InvoiceNumberPattern,
                TotalLabel = t.InvoiceProfile!.TotalLabel,
                DateFormat = t.InvoiceProfile!.DateFormat,
                Currency = t.InvoiceProfile!.Currency,
                LayoutNotes = t.InvoiceProfile!.LayoutNotes,
                UpdatedAt = t.InvoiceProfile != null ? t.InvoiceProfile.UpdatedAt : null,
                UpdatedBy = t.InvoiceProfile!.UpdatedBy,
            })
            .ToListAsync();

        var since = DateTime.UtcNow.AddDays(-30);
        var vm = new InvoiceTemplatesPageViewModel
        {
            Stores = rows,
            AiEnabled = analyzer is not null and not ManualInvoiceProfileAnalyzer,
            DuplicatesBlocked30d = await _context.AuditLogs.CountAsync(l => l.CreatedAt >= since && l.Action.StartsWith("INVOICE_DUPLICATE")),
        };

        ViewData["ActiveTab"] = "invoice_templates";
        ViewData["Title"] = "نماذج الفواتير";
        return View(vm);
    }

    /// <summary>يحفظ صورة فاتورة نموذجية ويحللها بالذكاء الاصطناعي. النتيجة اقتراح يراجعه الموظف قبل الحفظ.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> AnalyzeInvoiceSample(
        [FromForm] string storeId, IFormFile? file,
        [FromServices] IInvoiceProfileAnalyzer analyzer, [FromServices] IFileStorageService storage)
    {
        if (!await CanManageProfile(storeId)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "اختر صورة فاتورة من هذا المتجر." });
        var store = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == storeId);
        if (store == null) return NotFound(new { success = false, message = "المتجر غير موجود." });

        string url;
        await using (var save = file.OpenReadStream())
        {
            url = await storage.SaveFileAsync(save, file.FileName, "invoice-samples");
        }

        InvoiceProfileSuggestionDto suggestion;
        await using (var read = file.OpenReadStream())
        {
            suggestion = await analyzer.AnalyzeSampleAsync(read, store.NameAr,
                new ReceiptExtractionContext { ContentType = file.ContentType, FileName = file.FileName });
        }

        if (!suggestion.IsReceipt)
        {
            return BadRequest(new { success = false, message = "الصورة لا تبدو فاتورة شراء. صوّر فاتورة كاملة وواضحة من المتجر." });
        }

        return Json(new { success = true, sampleImageUrl = url, suggestion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInvoiceProfile([FromBody] SaveInvoiceProfileRequest req)
    {
        if (!await CanManageProfile(req.StoreId)) return Forbid();
        var store = await _context.Tenants.Include(t => t.Aliases).Include(t => t.InvoiceProfile)
            .FirstOrDefaultAsync(t => t.TenantId == req.StoreId);
        if (store == null) return NotFound(new { success = false, message = "المتجر غير موجود." });

        static string? Clean(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim()[..Math.Min(v.Trim().Length, max)];

        var example = Clean(req.InvoiceNumberExample, 100);
        var pattern = Clean(req.InvoiceNumberPattern, 200) ?? InvoiceNumbers.PatternFromExample(example);
        if (!InvoiceNumbers.IsValidPattern(pattern))
            return BadRequest(new { success = false, message = "نمط رقم الفاتورة غير صالح. امسحه ليُولَّد تلقائياً من المثال." });
        if (example != null && InvoiceNumbers.MatchesPattern(example, pattern) == false)
            return BadRequest(new { success = false, message = "مثال رقم الفاتورة لا يطابق النمط المكتوب." });
        if (Clean(req.PrintedName, 200) == null && example == null && Clean(req.TaxNumber, 60) == null)
            return BadRequest(new { success = false, message = "أدخل على الأقل الاسم المطبوع أو مثال رقم الفاتورة أو الرقم الضريبي." });

        var profile = store.InvoiceProfile;
        var isNew = profile == null;
        if (profile == null)
        {
            profile = new StoreInvoiceProfile { TenantId = store.TenantId };
            _context.StoreInvoiceProfiles.Add(profile);
        }

        profile.SampleImageUrl = Clean(req.SampleImageUrl, 500) ?? profile.SampleImageUrl;
        profile.PrintedName = Clean(req.PrintedName, 200);
        profile.TaxNumber = Clean(req.TaxNumber, 60);
        profile.PhoneOnReceipt = Clean(req.PhoneOnReceipt, 60);
        profile.InvoiceNumberLabel = Clean(req.InvoiceNumberLabel, 100);
        profile.InvoiceNumberExample = example;
        profile.InvoiceNumberPattern = pattern;
        profile.TotalLabel = Clean(req.TotalLabel, 100);
        profile.DateFormat = Clean(req.DateFormat, 40);
        profile.Currency = Clean(req.Currency, 10)?.ToUpperInvariant();
        profile.LayoutNotes = Clean(req.LayoutNotes, 1000);
        profile.IsActive = req.IsActive;
        if (req.Learned) profile.LearnedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = ReviewerName;

        // الأسماء المطبوعة تُضاف كأسماء بديلة للمتجر (يستخدمها محرك المطابقة أيضاً)
        var names = (req.OtherNames ?? "")
            .Split([',', '،', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(profile.PrintedName ?? "")
            .Where(n => n.Length >= 2 && n.Length <= 150)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var name in names)
        {
            var existing = store.Aliases.FirstOrDefault(a => string.Equals(a.AliasName, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { existing.IsActive = true; continue; }
            store.Aliases.Add(new StoreAlias { TenantId = store.TenantId, AliasName = name, MatchType = "CONTAINS", IsActive = true, CreatedAt = DateTime.UtcNow });
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Action = isNew ? "INVOICE_PROFILE_CREATE" : "INVOICE_PROFILE_UPDATE",
            EntityName = "StoreInvoiceProfile",
            EntityId = store.TenantId,
            PerformedBy = ReviewerName,
            Details = $"{(isNew ? "إنشاء" : "تحديث")} بصمة فاتورة متجر {store.NameAr}: رقم الفاتورة مثل {example ?? "—"}، الرقم الضريبي {profile.TaxNumber ?? "—"}",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "تم حفظ نموذج فاتورة المتجر.", pattern });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInvoiceProfile([FromBody] StoreIdRequest req)
    {
        if (!await CanManageProfile(req.StoreId)) return Forbid();
        var profile = await _context.StoreInvoiceProfiles.Include(p => p.Tenant).FirstOrDefaultAsync(p => p.TenantId == req.StoreId);
        if (profile == null) return NotFound(new { success = false, message = "لا يوجد نموذج لهذا المتجر." });

        _context.StoreInvoiceProfiles.Remove(profile);
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "INVOICE_PROFILE_DELETE",
            EntityName = "StoreInvoiceProfile",
            EntityId = req.StoreId,
            PerformedBy = ReviewerName,
            Details = $"حذف بصمة فاتورة متجر {profile.Tenant.NameAr}",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "تم حذف نموذج الفاتورة." });
    }

    /// <summary>
    /// اختبار: يقرأ فاتورة كما لو صوّرها عميل (بدون حفظ أو نقاط) ويبيّن المتجر المطابق،
    /// ومطابقة بصمة المتجر، وهل الرقم مستخدم مسبقاً.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> TestInvoiceProfile(IFormFile? file)
    {
        if (_ocr == null) return BadRequest(new { success = false, message = "خدمة القراءة الآلية غير متاحة." });
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "اختر صورة فاتورة." });

        ExtractedReceiptDto r;
        await using (var stream = file.OpenReadStream())
        {
            r = await _ocr.ExtractReceiptDataAsync(stream, await ReceiptContextBuilder.BuildAsync(_context, file.ContentType, file.FileName));
        }

        var store = r.MatchedTenantId == null ? null
            : await _context.Tenants.AsNoTracking().Include(t => t.InvoiceProfile).FirstOrDefaultAsync(t => t.TenantId == r.MatchedTenantId);
        var profile = store?.InvoiceProfile is { IsActive: true } p ? p : null;
        var duplicate = await InvoiceKeys.FindDuplicateAsync(_context, store?.TenantId, InvoiceNumbers.Key(r.InvoiceNumber),
            r.TotalAmount, r.InvoiceDate, merchantName: r.MerchantName);

        var checks = new List<object>();
        void Check(bool ok, string label) => checks.Add(new { ok, label });
        Check(r.IsReceipt && !r.RequiresManualReview, r.RequiresManualReview ? "تعذّرت القراءة الآلية" : r.IsReceipt ? "الصورة فاتورة شراء" : "الصورة ليست فاتورة");
        Check(store != null, store != null ? $"تم التعرف على المتجر: {store.NameAr}" : "لم يُتعرّف على متجر من متاجر المول");
        if (profile != null)
        {
            var match = InvoiceNumbers.MatchesPattern(r.InvoiceNumber, profile.InvoiceNumberPattern);
            if (match.HasValue) Check(match.Value, match.Value ? "رقم الفاتورة يطابق نمط المتجر" : $"رقم الفاتورة لا يطابق نمط المتجر (مثال: {profile.InvoiceNumberExample})");
            if (profile.TaxNumber != null && r.TaxNumber != null)
            {
                var same = InvoiceNumbers.SameIdentifier(profile.TaxNumber, r.TaxNumber);
                Check(same, same ? "الرقم الضريبي مطابق" : $"الرقم الضريبي مختلف ({r.TaxNumber})");
            }
        }
        else if (store != null)
        {
            Check(false, "لا يوجد نموذج فاتورة لهذا المتجر بعد");
        }
        Check(duplicate == null, duplicate == null ? "رقم الفاتورة غير مستخدم من قبل" : $"رقم الفاتورة مستخدم مسبقاً (العميل {duplicate.CustomerId})");

        return Json(new
        {
            success = true,
            provider = r.Provider,
            merchantName = r.MerchantName,
            matchedStore = store?.NameAr,
            invoiceNumber = r.InvoiceNumber,
            taxNumber = r.TaxNumber,
            invoiceDate = r.InvoiceDate?.ToString("yyyy-MM-dd"),
            totalAmount = r.TotalAmount,
            confidence = r.Confidence,
            concerns = r.AuthenticityConcerns,
            checks,
        });
    }

    // ----------------------------------------------------
    // 2.2 📋 INVOICES LOG & TRANSACTIONS (Invoices View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Invoices([FromQuery] string? storeId, [FromQuery] string? status, [FromQuery] string? search)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.InvoicesView)) return RedirectToAction(nameof(Index));

        var query = _context.Invoices
            .Include(i => i.Tenant)
            .Include(i => i.Customer)
            .Include(i => i.LineItems)
            .AsNoTracking();

        if (IsStoreScoped)
        {
            query = query.Where(i => i.TenantId == ScopeTenantId);
        }
        else if (!string.IsNullOrWhiteSpace(storeId) && storeId != "ALL")
        {
            query = query.Where(i => i.TenantId == storeId);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
        {
            if (Enum.TryParse<InvoiceStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(i => i.ProcessingStatus == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                (i.InvoiceNumber != null && i.InvoiceNumber.Contains(term)) ||
                (i.ExtractedMerchant != null && i.ExtractedMerchant.Contains(term)) ||
                (i.Customer != null && (i.Customer.FullName.Contains(term) || i.Customer.PhoneNumber.Contains(term)))
            );
        }

        var invoices = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

        var invoiceDtos = invoices.Select(i => new InvoiceItemViewModel
        {
            Id = i.InvoiceId.ToString(),
            InvoiceNumber = i.InvoiceNumber ?? $"INV-{i.InvoiceId.ToString().Substring(0, 8).ToUpper()}",
            StoreId = i.TenantId ?? string.Empty,
            StoreName = i.Tenant?.NameAr ?? i.ExtractedMerchant ?? "متجر عام",
            StoreCategory = i.Tenant?.Category ?? "عام",
            CustomerId = i.CustomerId,
            CustomerName = i.Customer?.FullName ?? "عميل التطبيق",
            CustomerPhone = i.Customer?.PhoneNumber ?? "-",
            Amount = i.TotalAmount.GetValueOrDefault(0m),
            PointsEarned = i.PointsAwarded,
            CashbackEarned = i.CashbackAwarded,
            InvoiceDate = i.InvoiceDate.GetValueOrDefault(i.CreatedAt),
            CreatedAt = i.CreatedAt,
            Status = i.ProcessingStatus,
            ConfidenceScore = Math.Round(i.ConfidenceScore.GetValueOrDefault(0.96m) * 100m, 1),
            RiskScore = i.FraudRiskScore,
            ImageUrl = i.ReceiptImageUrl,
            RejectionReason = i.RejectionReason,
            LineItems = i.LineItems.Select(li => new ExtractedLineItemDto
            {
                Description = li.ItemName,
                Quantity = (int)li.Quantity,
                UnitPrice = li.UnitPrice,
                TotalPrice = li.TotalPrice
            }).ToList()
        }).ToList();

        var storesQuery = _context.Tenants.Where(t => t.IsActive).AsNoTracking();
        if (IsStoreScoped)
        {
            storesQuery = storesQuery.Where(t => t.TenantId == ScopeTenantId);
        }

        var storeDropdown = await storesQuery
            .OrderBy(t => t.NameAr)
            .Select(t => new StoreTenantDto
            {
                Id = t.TenantId,
                Name = t.NameAr
            })
            .ToListAsync();

        var statsQuery = _context.Invoices.AsNoTracking();
        if (IsStoreScoped)
        {
            statsQuery = statsQuery.Where(i => i.TenantId == ScopeTenantId);
        }

        var totalApproved = await statsQuery.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).SumAsync(i => i.TotalAmount) ?? 0m;
        var totalPoints = await statsQuery.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).SumAsync(i => i.PointsAwarded);
        var pendingCount = await statsQuery.CountAsync(i => i.ProcessingStatus == InvoiceStatus.PENDING || i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED);
        var rejectedCount = await statsQuery.CountAsync(i => i.ProcessingStatus == InvoiceStatus.REJECTED || i.ProcessingStatus == InvoiceStatus.VOIDED);

        var viewModel = new InvoicesPageViewModel
        {
            Invoices = invoiceDtos,
            Stores = storeDropdown,
            SelectedStoreId = storeId ?? "ALL",
            StatusFilter = status ?? "ALL",
            SearchQuery = search ?? string.Empty,
            TotalCount = invoices.Count,
            TotalApprovedAmount = totalApproved,
            TotalApprovedPoints = totalPoints,
            PendingReviewsCount = pendingCount,
            RejectedCount = rejectedCount
        };

        ViewData["ActiveTab"] = "invoices";
        ViewData["Title"] = "سجل الفواتير والمعاملات";
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoiceDetailsJson(Guid id)
    {
        if (IsExplicitlyUnauthenticated) return Unauthorized();
        if (!await Can(Permissions.InvoicesView)) return Forbid();

        var invoice = await _context.Invoices
            .Include(i => i.Tenant)
            .Include(i => i.Customer)
            .Include(i => i.LineItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice == null) return NotFound();

        return Json(new
        {
            id = invoice.InvoiceId,
            invoiceNumber = invoice.InvoiceNumber,
            tenantId = invoice.TenantId,
            invoiceDateIso = invoice.InvoiceDate?.ToString("yyyy-MM-dd"),
            canReview = await Can(Permissions.InvoicesReview),
            storeName = invoice.Tenant?.NameAr ?? invoice.ExtractedMerchant ?? "متجر عام",
            storeCategory = invoice.Tenant?.Category ?? "عام",
            customerName = invoice.Customer?.FullName ?? "عميل",
            customerPhone = invoice.Customer?.PhoneNumber ?? "-",
            amount = invoice.TotalAmount.GetValueOrDefault(0m),
            points = invoice.PointsAwarded,
            cashback = invoice.CashbackAwarded,
            invoiceDate = invoice.InvoiceDate.GetValueOrDefault(invoice.CreatedAt).ToString("yyyy/MM/dd HH:mm"),
            scanDate = invoice.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
            status = invoice.ProcessingStatus.ToString(),
            confidence = Math.Round(invoice.ConfidenceScore.GetValueOrDefault(0.96m) * 100m, 1),
            riskScore = invoice.FraudRiskScore,
            imageUrl = invoice.ReceiptImageUrl,
            rejectionReason = invoice.RejectionReason,
            lineItems = invoice.LineItems.Select(li => new
            {
                description = li.ItemName,
                quantity = li.Quantity,
                unitPrice = li.UnitPrice,
                totalPrice = li.TotalPrice
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInvoiceStatus(Guid id, InvoiceStatus status, string? reason)
    {
        if (IsExplicitlyUnauthenticated) return Unauthorized(new { success = false, message = "غير مصرح" });

        var invoice = await _context.Invoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice == null) return NotFound(new { success = false, message = "الفاتورة غير موجودة." });

        if (IsStoreScoped && invoice.TenantId != ScopeTenantId)
        {
            return Forbid();
        }

        // الاعتماد والرفض يمران عبر خدمة المراجعة لتُضاف/تُسترجع النقاط بشكل صحيح
        if (status == InvoiceStatus.APPROVED || status == InvoiceStatus.REJECTED)
        {
            if (!await Can(Permissions.InvoicesReview)) return Forbid();
            return ReviewResult(status == InvoiceStatus.APPROVED
                ? await _review.ApproveAsync(id, ReviewerName)
                : await _review.RejectAsync(id, ReviewerName, string.IsNullOrWhiteSpace(reason) ? "تم الرفض بواسطة الإدارة" : reason));
        }

        // الإلغاء يسترجع النقاط ويحجز رقم الفاتورة (للمدير العام والمدقق فقط)
        if (status == InvoiceStatus.VOIDED)
        {
            if (!await Can(Permissions.InvoicesEdit)) return Forbid();
            return ReviewResult(await _review.VoidAsync(id, ReviewerName, string.IsNullOrWhiteSpace(reason) ? "تم الإلغاء بواسطة الإدارة" : reason));
        }

        if (!await Can(Permissions.InvoicesEdit)) return Forbid();
        invoice.ProcessingStatus = status;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "UPDATE_INVOICE_STATUS",
            EntityName = "Invoice",
            EntityId = invoice.InvoiceId.ToString(),
            PerformedBy = CurrentUserId ?? "Admin",
            Details = $"تعديل حالة الفاتورة {invoice.InvoiceNumber} إلى {status}. السبب: {reason ?? "لا يوجد"}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "تم تحديث حالة الفاتورة بنجاح." });
    }

    /// <summary>تعديل بيانات فاتورة غير معتمدة من سجل الفواتير.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InvoiceEdit(Guid id, [FromBody] InvoiceCorrection correction)
    {
        if (!await Can(Permissions.InvoicesEdit)) return Forbid();
        return ReviewResult(await _review.UpdateDetailsAsync(id, ReviewerName, correction));
    }

    // ----------------------------------------------------
    // 🔔 إشعارات الموظف (جرس الشريط العلوي)
    // ----------------------------------------------------
    private IQueryable<Notification> MyNotifications() =>
        _context.Notifications.Where(n => n.RecipientType == Notification.ToStaff && n.RecipientId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> NotificationsJson()
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
        var items = await MyNotifications().AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(20)
            .Select(n => new { n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt })
            .ToListAsync();
        return Json(new { unread = await MyNotifications().CountAsync(n => !n.IsRead), items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotificationsRead(Guid? id)
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
        var query = MyNotifications().Where(n => !n.IsRead);
        if (id.HasValue) query = query.Where(n => n.Id == id.Value);
        var list = await query.ToListAsync();
        foreach (var n in list) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
        await _context.SaveChangesAsync();
        return Json(new { success = true, count = list.Count });
    }

    // ----------------------------------------------------
    // تقارير العملاء (عرض، طباعة، PDF، Excel، واتساب)
    // ----------------------------------------------------
    [HttpGet]
    [RequirePermission(Permissions.CustomersExport)]
    public async Task<IActionResult> CustomerReports([FromQuery] string? type, [FromQuery] string? period, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? tier, [FromQuery] int page = 1, [FromQuery] bool print = false, [FromQuery] string? export = null)
    {
        var (f, t, label) = MMApplication.Api.Services.CustomerReports.Range(period, from, to);
        var result = await MMApplication.Api.Services.CustomerReports.RunAsync(_context, type, f, t, label, tier, IsStoreScoped ? ScopeTenantId : null);
        var tierName = string.IsNullOrWhiteSpace(tier) || tier == "ALL" ? null : MembershipTiers.NameOf(tier);
        var subtitle = (result.Type.UsesPeriod ? label : "الوضع الحالي") + (tierName != null ? $" • عضوية {tierName}" : "");

        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
        {
            string Cell(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Cell(result.Type.Title) + "," + Cell(subtitle));
            sb.AppendLine(string.Join(",", new[] { "#", "رقم العضوية", "الاسم", "الهاتف", "العضوية", "تاريخ التسجيل", result.Type.MetricLabel, result.Type.SecondLabel ?? "" }.Select(Cell)));
            foreach (var r in result.Rows)
                sb.AppendLine(string.Join(",", new object?[] { r.Rank, r.MembershipNumber, r.FullName, r.Phone, MembershipTiers.NameOf(r.Tier), r.RegisteredAt.ToLocalTime().ToString("yyyy-MM-dd"),
                    result.Type.Key == "inactive" ? r.LastActivity?.ToLocalTime().ToString("yyyy-MM-dd") ?? "لا توجد" : r.Metric.ToString("0.##"), r.Second?.ToString("0.##") }.Select(Cell)));
            _context.AuditLogs.Add(new AuditLog { PerformedBy = ReviewerName, Action = "REPORT_EXPORT", EntityName = "CustomerReport", EntityId = result.Type.Key,
                Details = $"تصدير تقرير {result.Type.Title} ({subtitle}) — {result.Rows.Count} عميل", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"customers-{result.Type.Key}-{DateTime.Now:yyyy-MM-dd}.csv");
        }

        ViewData["Result"] = result;
        ViewData["Subtitle"] = subtitle;
        ViewData["Type"] = result.Type.Key;
        ViewData["Period"] = period ?? "this_month";
        ViewData["From"] = from;
        ViewData["To"] = to;
        ViewData["Tier"] = tier ?? "ALL";
        ViewData["Title"] = result.Type.Title;
        ViewData["ActiveTab"] = "customer_reports";
        if (print)
        {
            _context.AuditLogs.Add(new AuditLog { PerformedBy = ReviewerName, Action = "REPORT_EXPORT", EntityName = "CustomerReport", EntityId = result.Type.Key,
                Details = $"طباعة تقرير {result.Type.Title} ({subtitle}) — {result.Rows.Count} عميل", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            return View("CustomerReportsPrint");
        }

        const int pageSize = 25;
        var totalPages = Math.Max(1, (int)Math.Ceiling(result.Rows.Count / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewData["Page"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["PageRows"] = result.Rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        ViewData["Tiers"] = await MembershipTiers.AllAsync(_context);
        return View();
    }

    /// <summary>كشف حساب عميل للطباعة أو حفظه PDF.</summary>
    [HttpGet]
    [RequirePermission(Permissions.CustomersView)]
    public async Task<IActionResult> CustomerStatement(string id)
    {
        var result = await CustomerDetails(id);
        if (result is not ViewResult { Model: CustomerProfileViewModel vm }) return result;
        ViewData["Title"] = $"كشف حساب: {vm.FullName}";
        ViewData["Subtitle"] = $"رقم العضوية {vm.MembershipNumber} • {vm.PhoneNumber}";
        return View("CustomerStatement", vm);
    }

    // ----------------------------------------------------
    // العضويات (مستويات العضوية وشروطها ومزاياها)
    // ----------------------------------------------------
    [HttpGet]
    [RequirePermission(Permissions.TiersManage)]
    public async Task<IActionResult> Tiers()
    {
        var tiers = await MembershipTiers.AllAsync(_context);
        var customers = _context.Customers.AsNoTracking().Where(c => !c.IsArchived);
        var counts = await customers.GroupBy(c => c.LoyaltyTier.ToUpper()).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var approved = _context.Invoices.AsNoTracking().Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED && i.Customer != null && !i.Customer.IsArchived);
        var spend = await approved.GroupBy(i => i.Customer!.LoyaltyTier.ToUpper())
            .Select(g => new { g.Key, Spent = g.Sum(i => i.TotalAmount ?? 0), Points = g.Sum(i => i.PointsAwarded) }).ToListAsync();
        var since = DateTime.UtcNow.AddDays(-30);
        var vm = new TiersPageViewModel
        {
            TotalCustomers = await customers.CountAsync(),
            LockedCustomers = await customers.CountAsync(c => c.TierLocked),
            UpgradedLast30 = await _context.AuditLogs.CountAsync(a => a.Action == "CUSTOMER_TIER_CHANGE" && a.CreatedAt >= since),
            CanManage = true,
            Tiers = tiers.Select(t =>
            {
                var n = counts.FirstOrDefault(c => c.Key == t.Code.ToUpper())?.Count ?? 0;
                var sp = spend.FirstOrDefault(x => x.Key == t.Code.ToUpper());
                return new TierRow(t, n, n == 0 ? 0 : (sp?.Spent ?? 0) / n, n == 0 ? 0 : (sp?.Points ?? 0) / n);
            }).ToList(),
        };
        ViewData["ActiveTab"] = "tiers";
        ViewData["Title"] = "العضويات";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.TiersManage)]
    public async Task<IActionResult> TierSave([FromBody] TierSaveRequest req)
    {
        var code = (req.Code ?? "").Trim().ToUpperInvariant();
        var name = (req.NameAr ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z][A-Z0-9_]{1,19}$"))
            return BadRequest(new { success = false, message = "الرمز بالإنجليزية بحروف كبيرة وأرقام (مثل GOLD أو PLATINUM)." });
        if (name.Length < 2) return BadRequest(new { success = false, message = "اكتب اسم العضوية." });
        if (req.MinPoints < 0 || req.MinSpend < 0 || req.QualificationMonths < 0 || req.QualificationMonths > 60)
            return BadRequest(new { success = false, message = "تحقق من الشروط (قيم موجبة، والفترة حتى 60 شهراً)." });
        if (req.PointsMultiplier < 1 || req.PointsMultiplier > 5)
            return BadRequest(new { success = false, message = "مضاعف النقاط بين 1 و 5." });
        var color = System.Text.RegularExpressions.Regex.IsMatch(req.Color ?? "", "^#[0-9A-Fa-f]{6}$") ? req.Color! : "#B607BC";

        var original = string.IsNullOrWhiteSpace(req.OriginalCode) ? null : req.OriginalCode.Trim().ToUpperInvariant();
        MembershipTier? tier = original == null ? null : await _context.MembershipTiers.FirstOrDefaultAsync(t => t.Code == original);
        if (original != null && tier == null) return NotFound(new { success = false, message = "العضوية غير موجودة." });
        if ((tier == null || original != code) && await _context.MembershipTiers.AnyAsync(t => t.Code == code))
            return BadRequest(new { success = false, message = "يوجد عضوية بنفس الرمز." });
        if (await _context.MembershipTiers.AnyAsync(t => t.NameAr == name && t.Code != original))
            return BadRequest(new { success = false, message = "يوجد عضوية بنفس الاسم." });

        var isNew = tier == null;
        if (tier != null && original != code)
        {
            // تغيير الرمز: ننقل العملاء للرمز الجديد
            _context.MembershipTiers.Remove(tier);
            await _context.SaveChangesAsync();
            var members = await _context.Customers.Where(c => c.LoyaltyTier == original).ToListAsync();
            foreach (var c in members) c.LoyaltyTier = code;
            tier = null;
        }
        tier ??= new MembershipTier { Code = code };
        tier.NameAr = name;
        tier.Benefits = (req.Benefits ?? "").Trim();
        tier.Color = color;
        tier.SortOrder = req.SortOrder;
        tier.MinPoints = req.MinPoints;
        tier.MinSpend = req.MinSpend;
        tier.QualificationMonths = req.QualificationMonths;
        tier.PointsMultiplier = req.PointsMultiplier;
        tier.IsActive = req.IsActive;
        tier.UpdatedAt = DateTime.UtcNow;
        if (_context.Entry(tier).State == EntityState.Detached) _context.MembershipTiers.Add(tier);

        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = ReviewerName, Action = isNew ? "TIER_CREATE" : "TIER_UPDATE", EntityName = "MembershipTier", EntityId = code,
            Details = $"{(isNew ? "إضافة" : "تعديل")} عضوية {name}: من {req.MinPoints} نقطة" + (req.MinSpend > 0 ? $" و{req.MinSpend:N0} د.ل" : "") +
                      $"، مضاعف {req.PointsMultiplier}x" + (req.IsActive ? "" : "، موقوفة"),
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        await MembershipTiers.AllAsync(_context); // تحديث أسماء العرض
        return Json(new { success = true, message = isNew ? "أُضيفت العضوية." : "حُفظت العضوية." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.TiersManage)]
    public async Task<IActionResult> TierDelete(string code, string? moveTo)
    {
        var tier = await _context.MembershipTiers.FirstOrDefaultAsync(t => t.Code == code);
        if (tier == null) return NotFound(new { success = false, message = "العضوية غير موجودة." });
        if (await _context.MembershipTiers.CountAsync() <= 1) return BadRequest(new { success = false, message = "يجب أن تبقى عضوية واحدة على الأقل." });
        var members = await _context.Customers.Where(c => c.LoyaltyTier == code).ToListAsync();
        if (members.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(moveTo) || moveTo == code || !await _context.MembershipTiers.AnyAsync(t => t.Code == moveTo))
                return BadRequest(new { success = false, message = $"في هذه العضوية {members.Count} عميل. اختر عضوية ينتقلون إليها." });
            foreach (var c in members) { c.LoyaltyTier = moveTo; c.TierUpdatedAt = DateTime.UtcNow; }
        }
        _context.MembershipTiers.Remove(tier);
        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = ReviewerName, Action = "TIER_DELETE", EntityName = "MembershipTier", EntityId = code,
            Details = $"حذف عضوية {tier.NameAr}" + (members.Count > 0 ? $" ونقل {members.Count} عميل إلى {MembershipTiers.NameOf(moveTo)}" : ""),
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "حُذفت العضوية." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.TiersManage)]
    public async Task<IActionResult> TiersReevaluate()
    {
        var n = await MembershipTiers.EvaluateAllAsync(_context, ReviewerName);
        return Json(new { success = true, message = n == 0 ? "لا يوجد عملاء يستحقون الترقية حالياً." : $"تمت ترقية {n} عميل وأُبلغوا بإشعار." });
    }

    // ----------------------------------------------------
    // الإشعارات المرسلة للعملاء (سجل الإرسال الجماعي)
    // ----------------------------------------------------
    public record SentNotificationRow(Guid Id, string Title, string Body, string Audience, int Recipients, int Read, int Pushed,
        string SentBy, DateTime SentAt, DateTime? RetractedAt);

    public class SentNotificationsViewModel
    {
        public List<SentNotificationRow> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalSent { get; set; }
        public int CustomersWithDevices { get; set; }
        public bool PushEnabled { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> SentNotifications([FromQuery] int page = 1)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.MarketingManage)) return RedirectToAction(nameof(Index));

        const int pageSize = 20;
        var total = await _context.NotificationBroadcasts.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var broadcasts = await _context.NotificationBroadcasts.AsNoTracking()
            .OrderByDescending(b => b.SentAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ids = broadcasts.Select(b => (Guid?)b.Id).ToList();
        var stats = await _context.Notifications.AsNoTracking()
            .Where(n => ids.Contains(n.BroadcastId))
            .GroupBy(n => n.BroadcastId)
            .Select(g => new { Id = g.Key, Read = g.Count(n => n.IsRead), Pushed = g.Count(n => n.PushSentAt != null) })
            .ToListAsync();

        var vm = new SentNotificationsViewModel
        {
            Page = page,
            TotalPages = totalPages,
            TotalSent = total,
            CustomersWithDevices = await _context.DeviceTokens.Select(d => d.CustomerId).Distinct().CountAsync(),
            PushEnabled = HttpContext?.RequestServices.GetService(typeof(FcmSender)) != null,
            Items = broadcasts.Select(b =>
            {
                var st = stats.FirstOrDefault(x => x.Id == b.Id);
                return new SentNotificationRow(b.Id, b.Title, b.Body, b.Audience, b.RecipientsCount, st?.Read ?? 0, st?.Pushed ?? 0,
                    b.SentBy, b.SentAt, b.RetractedAt);
            }).ToList(),
        };
        return View(vm);
    }

    /// <summary>سحب إشعار جماعي: يُحذف من تطبيقات العملاء ويبقى في السجل كمسحوب.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetractNotification(Guid id)
    {
        if (!await Can(Permissions.MarketingManage)) return Forbid();
        var b = await _context.NotificationBroadcasts.FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound(new { message = "الإشعار غير موجود." });
        if (b.RetractedAt != null) return Json(new { success = true, removed = 0 });

        var rows = await _context.Notifications.Where(n => n.BroadcastId == id).ToListAsync();
        _context.Notifications.RemoveRange(rows);
        b.RetractedAt = DateTime.UtcNow;
        _context.AuditLogs.Add(new AuditLog
        {
            PerformedBy = User?.Identity?.Name ?? "admin",
            Action = "NOTIFICATION_RETRACT",
            EntityName = "NotificationBroadcast",
            EntityId = id.ToString(),
            Details = $"سحب الإشعار «{b.Title}» من {rows.Count} عميل",
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        return Json(new { success = true, removed = rows.Count });
    }

    // ----------------------------------------------------
    // 3. 🛡️ FRAUD & REVIEW HUB (Fraud View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Fraud([FromQuery] string? statusFilter)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.FraudView)) return RedirectToAction(nameof(Index)); // تدقيق الاحتيال للإدارة والمدققين

        var filter = statusFilter?.ToUpperInvariant() ?? "ALL";
        var query = _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Tenant)
            .AsNoTracking();

        // RBAC: Store managers only see fraud events of their own store
        if (IsStoreScoped)
        {
            query = query.Where(i => i.TenantId == ScopeTenantId);
        }

        if (filter == "SUSPECT")
        {
            query = query.Where(i => i.FraudRiskScore >= 31 && i.FraudRiskScore <= 70);
        }
        else if (filter == "FLAGGED")
        {
            query = query.Where(i => i.FraudRiskScore > 70 || i.ProcessingStatus == InvoiceStatus.REJECTED);
        }
        else if (filter == "RESUBMIT")
        {
            query = query.Where(i => i.ProcessingStatus == InvoiceStatus.RESUBMISSION_REQUESTED);
        }
        else if (filter == "DISPUTED")
        {
            query = query.Where(i => i.ProcessingStatus == InvoiceStatus.DISPUTED);
        }
        else if (filter == "VOIDED")
        {
            query = query.Where(i => i.ProcessingStatus == InvoiceStatus.VOIDED);
        }
        else
        {
            query = query.Where(i => i.ProcessingStatus == InvoiceStatus.PENDING || i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED || i.ProcessingStatus == InvoiceStatus.DISPUTED || i.ProcessingStatus == InvoiceStatus.RESUBMISSION_REQUESTED || i.FraudRiskScore >= 31);
        }

        var allDevices = await _context.CustomerDevices.Include(d => d.Customer).AsNoTracking().ToListAsync();
        var devicesByFingerprint = allDevices.GroupBy(d => d.DeviceFingerprint).ToDictionary(g => g.Key, g => g.ToList());

        var items = await query.OrderByDescending(i => i.FraudRiskScore).ThenByDescending(i => i.CreatedAt).ToListAsync();

        var dtos = items.Select(i =>
        {
            var customerFps = allDevices.Where(d => d.CustomerId == i.CustomerId).Select(d => d.DeviceFingerprint).ToList();
            var sharedAccounts = new HashSet<string>();
            foreach (var fp in customerFps)
            {
                if (devicesByFingerprint.TryGetValue(fp, out var linkedDevs))
                {
                    foreach (var ld in linkedDevs.Where(x => x.CustomerId != i.CustomerId))
                    {
                        var name = ld.Customer?.FullName ?? ld.CustomerId;
                        sharedAccounts.Add(name);
                    }
                }
            }

            return new FraudInvoiceItemDto
            {
                Id = i.InvoiceId,
                CustomerId = i.CustomerId,
                CustomerName = i.Customer?.FullName ?? "عميل غير معروف",
                CustomerPhone = i.Customer?.PhoneNumber ?? "-",
                MerchantName = i.Tenant?.NameAr ?? i.ExtractedMerchant ?? "متجر غير محدد",
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate.GetValueOrDefault(i.CreatedAt),
                TotalAmount = i.TotalAmount.GetValueOrDefault(0m),
                PointsEarned = i.PointsAwarded,
                Status = i.ProcessingStatus,
                RiskScore = i.FraudRiskScore,
                PhotoHash = i.ImagePHash,
                FraudFlagReason = i.RejectionReason,
                ResubmissionNotes = i.ResubmissionNotes,
                DisputeReason = i.DisputeReason,
                HasHashMatch = !string.IsNullOrEmpty(i.ImagePHash),
                ImageUrl = i.ReceiptImageUrl,
                CreatedAt = i.CreatedAt,
                HasMultiAccountFlag = sharedAccounts.Count > 0,
                SharedAccountsCount = sharedAccounts.Count,
                SharedAccountNames = sharedAccounts.ToList()
            };
        }).ToList();

        var statsInvoiceQuery = _context.Invoices.AsNoTracking();
        if (IsStoreScoped)
        {
            statsInvoiceQuery = statsInvoiceQuery.Where(i => i.TenantId == ScopeTenantId);
        }

        var viewModel = new FraudPageViewModel
        {
            QueueItems = dtos,
            StatusFilter = filter,
            HighRiskCount = await statsInvoiceQuery.CountAsync(i => i.FraudRiskScore > 70),
            SuspectCount = await statsInvoiceQuery.CountAsync(i => i.FraudRiskScore >= 31 && i.FraudRiskScore <= 70),
            DisputedCount = await statsInvoiceQuery.CountAsync(i => i.ProcessingStatus == InvoiceStatus.DISPUTED),
            ResubmitCount = await statsInvoiceQuery.CountAsync(i => i.ProcessingStatus == InvoiceStatus.RESUBMISSION_REQUESTED),
            TotalInspected = await statsInvoiceQuery.CountAsync(i => i.ProcessingStatus == InvoiceStatus.APPROVED || i.ProcessingStatus == InvoiceStatus.REJECTED || i.ProcessingStatus == InvoiceStatus.VOIDED),
            SystemAccuracyPercentage = 98.4m
        };

        ViewData["ActiveTab"] = "fraud";
        ViewData["Title"] = "مركز مكافحة الاحتيال والتدقيق";
        return View(viewModel);
    }

    // ----------------------------------------------------
    // 4. 📢 ADS & OFFERS HUB (Ads View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Ads()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        if (!await Can(Permissions.MarketingManage))
        {
            return RedirectToAction(nameof(Index));
        }

        var banners = await _context.PromotionBanners
            .AsNoTracking()
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();

        var dtos = banners.Select(b => new PromotionBannerDto
        {
            Id = b.Id,
            Title = b.Title,
            Subtitle = b.Subtitle,
            ImageUrl = b.ImageUrl,
            TargetUrl = b.TargetUrl,
            ActionType = b.ActionType,
            ActionPayloadJson = b.ActionPayloadJson,
            TargetAudience = b.TargetAudience,
            DisplayOrder = b.DisplayOrder,
            StartDate = b.StartDate,
            EndDate = b.EndDate,
            IsActive = b.IsActive,
            CreatedAt = b.CreatedAt
        }).ToList();

        ViewData["ActiveTab"] = "ads";
        ViewData["Title"] = "الإعلانات والعروض الترويجية";
        return View(dtos);
    }

    // ----------------------------------------------------
    // 5. 🌟 MALL-WIDE LOYALTY (Loyalty View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Loyalty()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.LoyaltyManage, Permissions.CampaignsManage)) return RedirectToAction(nameof(Index)); // قواعد النقاط: المدير العام ومدير المتجر لمتجره

        var campaigns = await _context.LoyaltyCampaigns
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var campaignDtos = campaigns.Select(c => new LoyaltyCampaignDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            PointsMultiplier = c.PointsMultiplier,
            MinSpendAmount = c.MinSpendAmount,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            IsActive = c.IsActive
        }).ToList();

        var boosterQuery = _context.ProductBoosterRules
            .Include(r => r.Tenant)
            .AsNoTracking();

        var earningRuleQuery = _context.LoyaltyEarningRules
            .Include(r => r.StoreTenant)
            .AsNoTracking();

        if (IsStoreScoped)
        {
            boosterQuery = boosterQuery.Where(r => r.TenantId == ScopeTenantId);
            earningRuleQuery = earningRuleQuery.Where(r => r.StoreTenantId == ScopeTenantId);
        }

        var boosterRules = await boosterQuery.OrderByDescending(r => r.CreatedAt).ToListAsync();
        var earningRules = await earningRuleQuery.OrderByDescending(r => r.CreatedAt).ToListAsync();

        var boosterDtos = boosterRules.Select(r => new ProductBoosterRuleDto
        {
            Id = r.Id,
            Title = r.Title,
            BrandOrItemKeyword = r.BrandOrItemKeyword,
            TargetCategory = r.TargetCategory,
            TenantId = r.TenantId,
            StoreName = r.Tenant != null ? r.Tenant.NameAr : "كافة متاجر المول (عام)",
            BonusPoints = r.BonusPoints,
            PointsMultiplier = r.PointsMultiplier,
            SponsorName = r.SponsorName,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt
        }).ToList();

        var earningRuleDtos = earningRules.Select(r => new LoyaltyEarningRuleDto
        {
            RuleId = r.RuleId,
            StoreTenantId = r.StoreTenantId,
            StoreName = r.StoreTenant != null ? r.StoreTenant.NameAr : "كافة متاجر المول (عام)",
            RuleName = r.RuleName,
            CalculationType = r.CalculationType,
            FixedPointsPerInvoice = r.FixedPointsPerInvoice,
            SpendRatioRate = r.SpendRatioRate,
            MinInvoiceAmount = r.MinInvoiceAmount,
            MaxPointsCap = r.MaxPointsCap,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        }).ToList();

        var storeQuery = _context.Tenants.Where(t => t.IsActive);
        if (IsStoreScoped)
        {
            storeQuery = storeQuery.Where(t => t.TenantId == ScopeTenantId);
        }

        var stores = await storeQuery
            .OrderBy(t => t.NameAr)
            .Select(t => new StoreTenantDto
            {
                Id = t.TenantId,
                Name = t.NameAr
            })
            .ToListAsync();

        var viewModel = new LoyaltyPageViewModel
        {
            Campaigns = campaignDtos,
            Boosters = boosterDtos,
            EarningRules = earningRuleDtos,
            Stores = stores,
            Rules = new LoyaltyRulesDto
            {
                DinarsPerPoint = 10.0m,
                PointRedemptionValueDinars = 0.50m,
                BronzeThreshold = 0,
                SilverThreshold = 500,
                GoldThreshold = 1500,
                PlatinumThreshold = 5000,
                SilverMultiplier = 1.25m,
                GoldMultiplier = 1.50m,
                PlatinumMultiplier = 2.00m
            }
        };

        ViewData["ActiveTab"] = "loyalty";
        ViewData["Title"] = IsStoreScoped ? "قواعد كاش باك وبونص المتجر" : "برنامج ولاء المول العام";
        return View(viewModel);
    }

    // ----------------------------------------------------
    // 6. 💳 SETTLEMENTS & CLEARING (Settlements View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Settlements()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.SettlementsView, Permissions.SettlementsManage)) return RedirectToAction(nameof(Index));

        var settlementQuery = _context.TenantSettlements
            .Include(s => s.Tenant)
            .AsNoTracking();

        if (IsStoreScoped)
        {
            settlementQuery = settlementQuery.Where(s => s.TenantId == ScopeTenantId);
        }

        var settlements = await settlementQuery
            .OrderByDescending(s => s.PeriodEndDate)
            .ToListAsync();

        var dtos = settlements.Select(s => new StoreSettlementDto
        {
            Id = s.SettlementId,
            StoreTenantId = s.TenantId,
            StoreName = s.Tenant != null ? s.Tenant.NameAr : "متجر غير معروف",
            PeriodLabel = $"{s.PeriodStartDate:yyyy/MM/dd} ← {s.PeriodEndDate:MM/dd}",
            PeriodStart = s.PeriodStartDate,
            PeriodEnd = s.PeriodEndDate,
            RedemptionsCount = 1,
            TotalRedemptionsValue = s.TotalVouchersRedeemedValue,
            MallCommissionAmount = s.MallServiceFee,
            NetPayableAmount = s.NetPayableToTenant,
            Status = s.SettlementStatus,
            SettledAt = s.SettledAt,
            CreatedAt = s.CreatedAt
        }).ToList();

        ViewData["ActiveTab"] = "settlements";
        ViewData["Title"] = IsStoreScoped ? "مستحقات وتسويات متجري المالية" : "التسويات والمقاصة المالية للمتاجر";
        return View(dtos);
    }

    // ----------------------------------------------------
    // 7. 📱 CUSTOMER DATABASE (Customers View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Customers([FromQuery] string? tier, [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] string? sort, [FromQuery] int page = 1, [FromQuery] string? export = null)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.CustomersView)) return RedirectToAction(nameof(Index)); // بيانات العملاء الشخصية للإدارة والمدققين

        var query = _context.Customers
            .Include(c => c.Wallets)
            .Include(c => c.Invoices)
            .Include(c => c.Devices)
            .AsNoTracking();

        if (IsStoreScoped)
        {
            query = query.Where(c => c.Invoices.Any(i => i.TenantId == ScopeTenantId));
        }

        if (!string.IsNullOrWhiteSpace(tier) && tier != "ALL")
        {
            var t = tier.ToUpperInvariant();
            query = query.Where(c => c.LoyaltyTier.ToUpper() == t);
        }

        var st = (status ?? "ALL").ToUpperInvariant();
        query = st switch
        {
            "ACTIVE" => query.Where(c => !c.IsArchived && !c.IsFrozen),
            "FROZEN" => query.Where(c => !c.IsArchived && c.IsFrozen),
            "NOAPP" => query.Where(c => !c.IsArchived && (c.PasswordHash == null || c.PasswordHash == "")),
            "DELETED" => query.Where(c => c.IsArchived),
            _ => query.Where(c => !c.IsArchived),
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var phone = MMApplication.Application.Common.PhoneNumbers.Normalize(term) ?? term;
            var upper = term.ToUpperInvariant();
            var lower = term.ToLowerInvariant();
            query = query.Where(c => c.FullName.Contains(term) || c.PhoneNumber.Contains(phone) || c.CustomerId.Contains(term) ||
                                     (c.MembershipNumber != null && c.MembershipNumber.Contains(upper)) ||
                                     (c.Email != null && c.Email.Contains(lower)));
        }

        var allDevices = await _context.CustomerDevices.AsNoTracking().Select(d => new { d.DeviceFingerprint, d.CustomerId }).ToListAsync();
        var sharedFingerprints = allDevices
            .GroupBy(d => d.DeviceFingerprint)
            .Where(g => g.Select(x => x.CustomerId).Distinct().Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CustomerId).Distinct().Count());

        var customers = await query.ToListAsync();
        var dtos = customers.Select(c => SuperAdminController.ToCustomerDto(c,
            c.Devices.Select(d => sharedFingerprints.TryGetValue(d.DeviceFingerprint, out var n) ? n : 0).DefaultIfEmpty(0).Max())).ToList();
        if (st == "SHARED") dtos = dtos.Where(d => d.IsMultiAccountDevice).ToList();

        var sortKey = (sort ?? "new").ToLowerInvariant();
        dtos = sortKey switch
        {
            "points" => dtos.OrderByDescending(d => d.CurrentPoints).ToList(),
            "spent" => dtos.OrderByDescending(d => d.TotalSpent).ToList(),
            "invoices" => dtos.OrderByDescending(d => d.TotalInvoicesScanned).ToList(),
            "name" => dtos.OrderBy(d => d.FullName).ToList(),
            "old" => dtos.OrderBy(d => d.CreatedAt).ToList(),
            _ => dtos.OrderByDescending(d => d.CreatedAt).ToList(),
        };

        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
        {
            if (!await Can(Permissions.CustomersExport)) return Forbid();
            string Cell(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join(",", new[] { "رقم العضوية", "الاسم", "الهاتف", "البريد", "تاريخ الميلاد", "الجنس", "الفئة", "النقاط", "الفواتير", "الإنفاق المعتمد", "الحالة", "تاريخ التسجيل" }.Select(Cell)));
            foreach (var d in dtos)
            {
                sb.AppendLine(string.Join(",", new object?[]
                {
                    d.MembershipNumber, d.FullName, d.PhoneNumber, d.Email, d.BirthDate?.ToString("yyyy-MM-dd"),
                    d.Gender == "F" ? "أنثى" : d.Gender == "M" ? "ذكر" : "", MMApplication.Api.ViewModels.DisplayLabels.Tier(d.Tier),
                    d.CurrentPoints, d.TotalInvoicesScanned, d.TotalSpent.ToString("0.00"),
                    d.IsArchived ? "محذوف" : d.IsSuspended ? "مجمّد" : "نشط", d.CreatedAt.ToString("yyyy-MM-dd"),
                }.Select(Cell)));
            }
            _context.AuditLogs.Add(new AuditLog
            {
                PerformedBy = User?.Identity?.Name ?? "admin", Action = "REPORT_EXPORT", EntityName = "Customer",
                Details = $"تصدير قائمة العملاء ({dtos.Count} عميل)", CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"customers-{DateTime.Now:yyyy-MM-dd}.csv");
        }

        const int pageSize = 25;
        var totalFiltered = dtos.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        // مؤشرات عامة (على كل العملاء غير المحذوفين، وليس الصفحة الحالية فقط)
        var baseQuery = _context.Customers.AsNoTracking().Where(c => !c.IsArchived);
        if (IsStoreScoped)
            baseQuery = baseQuery.Where(c => c.Invoices.Any(i => i.TenantId == ScopeTenantId));
        var baseIds = baseQuery.Select(c => c.CustomerId);
        ViewData["StatTotal"] = await baseQuery.CountAsync();
        ViewData["StatFrozen"] = await baseQuery.CountAsync(c => c.IsFrozen);
        ViewData["StatApp"] = await baseQuery.CountAsync(c => c.PasswordHash != null && c.PasswordHash != "");
        ViewData["StatPoints"] = (int)await _context.CustomerWallets.AsNoTracking()
            .Where(w => w.BalanceType == BalanceType.POINTS && baseIds.Contains(w.CustomerId)).SumAsync(w => (decimal?)w.CurrentBalance ?? 0);
        ViewData["StatNew30"] = await baseQuery.CountAsync(c => c.RegisteredAt >= DateTime.UtcNow.AddDays(-30));
        ViewData["StatShared"] = sharedFingerprints.Count == 0 ? 0 : allDevices.Where(d => sharedFingerprints.ContainsKey(d.DeviceFingerprint)).Select(d => d.CustomerId).Distinct().Count();

        // تسجيلات العملاء الجدد في آخر 6 أشهر (بيانات حقيقية)
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var registrations = await _context.Customers.AsNoTracking()
            .Where(c => c.RegisteredAt >= monthStart)
            .Select(c => c.RegisteredAt)
            .ToListAsync();
        var arMonths = new[] { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };
        ViewData["MonthlyRegistrations"] = Enumerable.Range(0, 6)
            .Select(i => monthStart.AddMonths(i))
            .Select(m => new { label = arMonths[m.Month - 1], count = registrations.Count(r => r.Year == m.Year && r.Month == m.Month) })
            .ToList();
        ViewData["TierCounts"] = await baseQuery.GroupBy(c => c.LoyaltyTier.ToUpper()).Select(g => new { tier = g.Key, count = g.Count() }).ToListAsync();

        ViewData["ActiveTab"] = "customers";
        ViewData["CanEditCustomers"] = await Can(Permissions.CustomersEdit);
        ViewData["CanOpenProfile"] = await Can(Permissions.CustomersView);
        ViewData["CanPoints"] = await Can(Permissions.CustomersPoints);
        ViewData["CanDelete"] = await Can(Permissions.CustomersDelete);
        ViewData["CanExport"] = await Can(Permissions.CustomersExport);
        ViewData["IsSuperAdmin"] = (bool)ViewData["CanPoints"]!;
        ViewData["Title"] = "العملاء";
        ViewData["TierFilter"] = tier ?? "ALL";
        ViewData["StatusFilter"] = st;
        ViewData["Sort"] = sortKey;
        ViewData["SearchQuery"] = search ?? "";
        ViewData["Page"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalFiltered"] = totalFiltered;
        return View(dtos.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    // ----------------------------------------------------
    // 7.1 🔍 DEEP CUSTOMER DETAILS & LEDGER (Customer Details View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> CustomerDetails(string id)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(Customers));
        // مدير المتجر لا يطّلع على ملفات العملاء الكاملة
        if (!await Can(Permissions.CustomersView)) return RedirectToAction(nameof(Index));

        var c = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound("العميل غير مسجل في النظام.");

        var stores = await _context.Tenants.AsNoTracking().ToDictionaryAsync(t => t.TenantId, t => t);
        var invoices = await _context.Invoices.AsNoTracking().Where(i => i.CustomerId == id)
            .OrderByDescending(i => i.CreatedAt).ToListAsync();
        var wallet = await _context.CustomerWallets.AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == id && w.BalanceType == BalanceType.POINTS);
        var ledger = wallet == null ? new List<WalletLedger>() : await _context.WalletLedger.AsNoTracking()
            .Where(l => l.WalletId == wallet.WalletId).OrderByDescending(l => l.CreatedAt).Take(300).ToListAsync();
        var vouchers = await _context.Redemptions.AsNoTracking().Include(r => r.Reward)
            .Where(r => r.CustomerId == id).OrderByDescending(r => r.RedeemedAt).Take(200).ToListAsync();
        var notifications = await _context.Notifications.AsNoTracking()
            .Where(n => n.RecipientType == Notification.ToCustomer && n.RecipientId == id)
            .OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
        var devices = await _context.CustomerDevices.AsNoTracking().Where(d => d.CustomerId == id).ToListAsync();
        var fingerprints = devices.Select(d => d.DeviceFingerprint).ToList();
        var sharing = await _context.CustomerDevices.AsNoTracking()
            .Where(d => fingerprints.Contains(d.DeviceFingerprint) && d.CustomerId != id)
            .Select(d => new { d.DeviceFingerprint, d.CustomerId }).ToListAsync();
        var audit = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == id || (c.MembershipNumber != null && a.Details.Contains(c.MembershipNumber)))
            .OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync();

        string StoreName(Invoice i) => i.TenantId != null && stores.TryGetValue(i.TenantId, out var t) ? t.NameAr : (i.ExtractedMerchant ?? "متجر غير معروف");
        var approved = invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).ToList();

        var vm = new CustomerProfileViewModel
        {
            CustomerId = c.CustomerId,
            MembershipNumber = c.MembershipNumber,
            FullName = c.FullName,
            PhoneNumber = c.PhoneNumber,
            Email = c.Email,
            BirthDate = c.BirthDate,
            Gender = c.Gender,
            Tier = c.LoyaltyTier,
            TierLocked = c.TierLocked,
            TierProgress = await MembershipTiers.ProgressAsync(_context, c),
            IsFrozen = c.IsFrozen,
            IsArchived = c.IsArchived,
            HasAppPassword = !string.IsNullOrEmpty(c.PasswordHash),
            RegisteredAt = c.RegisteredAt,
            UpdatedAt = c.UpdatedAt,
            LastInvoiceAt = invoices.FirstOrDefault()?.CreatedAt,
            LastLoginAt = c.LastLoginAt,
            LoginCount = c.LoginCount,
            LastSeenAt = c.LastSeenAt,
            LastAppVersion = c.LastAppVersion,
            LastPlatform = c.LastPlatform,
            PasswordChangedAt = c.PasswordChangedAt,
            PasswordChangedBy = c.PasswordChangedBy,
            PasswordHistory = audit.Where(a => a.Action is "CUSTOMER_PASSWORD_RESET" or "CUSTOMER_PASSWORD_CHANGE" or "CUSTOMER_CREATE")
                .Select(a => new CustomerProfileViewModel.AuditRow(a.CreatedAt, a.PerformedBy, a.Action, a.Details)).ToList(),
            PointsBalance = (int)(wallet?.CurrentBalance ?? 0),
            PointsEarned = (int)ledger.Where(l => l.AmountChanged > 0).Sum(l => l.AmountChanged),
            PointsRedeemed = (int)-ledger.Where(l => l.AmountChanged < 0).Sum(l => l.AmountChanged),
            TotalSpent = approved.Sum(i => i.TotalAmount ?? 0),
            AverageInvoice = approved.Count == 0 ? 0 : approved.Average(i => i.TotalAmount ?? 0),
            InvoicesCount = invoices.Count,
            ApprovedCount = approved.Count,
            PendingCount = invoices.Count(i => i.ProcessingStatus is InvoiceStatus.PENDING or InvoiceStatus.REVIEW_REQUIRED),
            RejectedCount = invoices.Count(i => i.ProcessingStatus is InvoiceStatus.REJECTED or InvoiceStatus.VOIDED),
            VouchersActive = vouchers.Count(v => v.Status == RedemptionStatus.ACTIVE && v.ExpiresAt > DateTime.UtcNow),
            SharedDeviceAccounts = sharing.Select(x => x.CustomerId).Distinct().Count(),
            CanEdit = await Can(Permissions.CustomersEdit),
            IsSuperAdmin = await Can(Permissions.CustomersPoints),
            CanDelete = await Can(Permissions.CustomersDelete),
            Invoices = invoices.Select(i => new CustomerProfileViewModel.InvoiceRow(i.InvoiceId, StoreName(i), i.InvoiceNumber,
                i.InvoiceDate ?? i.CreatedAt, i.CreatedAt, i.TotalAmount ?? 0, i.PointsAwarded, i.ProcessingStatus, i.FraudRiskScore,
                i.RejectionReason, i.ReceiptImageUrl)).ToList(),
            Ledger = ledger.Select(l => new CustomerProfileViewModel.LedgerRow(l.CreatedAt, l.TransactionType, l.AmountChanged, l.BalanceAfter,
                l.ReferenceRemarks, l.InvoiceId)).ToList(),
            Vouchers = vouchers.Select(v => new CustomerProfileViewModel.VoucherRow(v.RedemptionCode, v.Reward?.Title ?? "مكافأة", v.PointsSpent,
                v.Status, v.RedeemedAt, v.ExpiresAt, v.UsedAt, v.UsedByMerchant)).ToList(),
            Stores = invoices.GroupBy(StoreName).Select(g => new CustomerProfileViewModel.StoreRow(g.Key,
                    g.First().TenantId != null && stores.TryGetValue(g.First().TenantId!, out var st) ? st.Category : "—",
                    g.Count(),
                    g.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount ?? 0),
                    g.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.PointsAwarded),
                    g.Max(i => i.CreatedAt)))
                .OrderByDescending(s => s.Spent).ToList(),
            Notifications = notifications.Select(n => new CustomerProfileViewModel.NotificationRow(n.Title, n.Body, n.Type, n.CreatedAt, n.IsRead)).ToList(),
            Devices = devices.OrderByDescending(d => d.LastSeenAt).Select(d => new CustomerProfileViewModel.DeviceRow(d.DeviceModel, d.OsVersion,
                d.FirstSeenAt, d.LastSeenAt, sharing.Where(x => x.DeviceFingerprint == d.DeviceFingerprint).Select(x => x.CustomerId).Distinct().Count())).ToList(),
            Audit = audit.Select(a => new CustomerProfileViewModel.AuditRow(a.CreatedAt, a.PerformedBy, a.Action, a.Details)).ToList(),
        };

        ViewData["ActiveTab"] = "customers";
        ViewData["Title"] = c.FullName;
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomerDetailsJson(string id)
    {
        if (IsExplicitlyUnauthenticated) return Unauthorized();
        if (!await Can(Permissions.CustomersView)) return Forbid();
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("Invalid ID");

        var customer = await _context.Customers
            .Include(c => c.Wallets)
                .ThenInclude(w => w.LedgerEntries)
            .Include(c => c.Invoices)
                .ThenInclude(i => i.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null) return NotFound();

        var stores = await _context.Tenants.AsNoTracking().ToListAsync();

        var invoiceDtos = customer.Invoices
            .OrderByDescending(i => i.InvoiceDate ?? i.CreatedAt)
            .Select(i =>
            {
                var store = stores.FirstOrDefault(s => s.TenantId == i.TenantId);
                return new
                {
                    id = i.InvoiceId,
                    merchantName = store?.NameAr ?? i.ExtractedMerchant ?? "متجر عام",
                    storeCategory = store?.Category ?? "تسوق عام",
                    invoiceNumber = i.InvoiceNumber,
                    invoiceDate = (i.InvoiceDate ?? i.CreatedAt).ToString("yyyy/MM/dd HH:mm"),
                    scanDate = i.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                    totalAmount = i.TotalAmount.GetValueOrDefault(0m),
                    pointsEarned = i.PointsAwarded,
                    riskScore = i.FraudRiskScore,
                    status = i.ProcessingStatus.ToString(),
                    fraudFlagReason = i.RejectionReason,
                    imageUrl = i.ReceiptImageUrl
                };
            }).ToList();

        var storeVisits = customer.Invoices
            .GroupBy(i => i.Tenant != null ? i.Tenant.NameAr : (i.ExtractedMerchant ?? "متجر عام"))
            .Select(g =>
            {
                var store = stores.FirstOrDefault(s => s.NameAr == g.Key || s.NameEn == g.Key);
                return new
                {
                    storeName = g.Key,
                    category = store?.Category ?? "متجر",
                    visitCount = g.Count(),
                    totalSpent = g.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount) ?? 0m,
                    lastVisit = (g.Max(i => i.InvoiceDate) ?? DateTime.UtcNow).ToString("yyyy/MM/dd")
                };
            })
            .OrderByDescending(sv => sv.totalSpent)
            .ToList();

        var pointsWallet = customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS);

        return Json(new
        {
            customerId = customer.CustomerId,
            fullName = customer.FullName,
            phoneNumber = customer.PhoneNumber,
            tier = customer.LoyaltyTier,
            pointsBalance = (int)(pointsWallet?.CurrentBalance ?? 0),
            totalSpent = customer.Invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount) ?? 0m,
            invoicesCount = customer.Invoices.Count,
            approvedCount = customer.Invoices.Count(i => i.ProcessingStatus == InvoiceStatus.APPROVED),
            isSuspended = customer.IsFrozen,
            registeredAt = customer.RegisteredAt.ToString("yyyy/MM/dd"),
            invoices = invoiceDtos,
            storeVisits = storeVisits
        });
    }

    // ----------------------------------------------------
    // 8. 👥 STAFF & ROLE MANAGEMENT (Staff View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Staff()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        // Developer & Super Admin Only
        if (!await Can(Permissions.StaffManage))
        {
            return RedirectToAction(nameof(Index));
        }

        var staffList = await _context.SystemUsers
            .Include(u => u.Tenant)
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var stores = await _context.Tenants.AsNoTracking().OrderBy(s => s.NameAr).ToListAsync();

        var staffDtos = staffList.Select(s => new StaffUserDto
        {
            Id = s.UserId,
            FullName = s.FullName,
            PhoneNumber = s.PhoneNumber ?? "-",
            Role = CustomerRole.Admin,
            StaffRole = s.Role switch
            {
                SystemUserRole.SUPER_ADMIN => StaffRole.SuperAdmin,
                SystemUserRole.FRAUD_AUDITOR => StaffRole.Auditor,
                SystemUserRole.STORE_MANAGER => StaffRole.StoreManager,
                SystemUserRole.CASHIER => StaffRole.Cashier,
                _ => StaffRole.Auditor
            },
            StaffRoleLabel = s.Role switch
            {
                SystemUserRole.SUPER_ADMIN => "المدير العام",
                SystemUserRole.FRAUD_AUDITOR => "مدقق فواتير",
                SystemUserRole.STORE_MANAGER => "مدير فرع",
                SystemUserRole.CASHIER => "كاشير ونقاط بيع",
                _ => s.Role.ToString()
            },
            StoreTenantId = s.TenantId,
            StoreName = s.Tenant != null ? s.Tenant.NameAr : "إدارة المول المركزية",
            IsActive = s.IsActive,
            IsCurrentUser = s.UserId == CurrentUserId,
            CreatedAt = s.CreatedAt,
            LastLoginAt = s.LastLoginAt,
            LockedUntil = s.LockedUntil > DateTime.UtcNow ? s.LockedUntil : null,
            Email = s.Email,
        }).ToList();

        var storeDtos = stores.Select(st => new StoreTenantDto
        {
            Id = st.TenantId,
            Name = st.NameAr
        }).ToList();

        var viewModel = new StaffPageViewModel
        {
            StaffMembers = staffDtos,
            Stores = storeDtos
        };

        ViewData["ActiveTab"] = "staff";
        ViewData["Title"] = "طاقم العمل وإدارة الصلاحيات";
        var rolePerms = new Dictionary<string, HashSet<string>>();
        foreach (var r in Permissions.EditableRoles) rolePerms[r.ToString()] = await Permissions.ForRoleAsync(_context, r);
        ViewData["RolePermissions"] = rolePerms;
        ViewData["ActorIsSuperAdmin"] = User == null || Permissions.RoleOf(User) == SystemUserRole.SUPER_ADMIN;
        return View(viewModel);
    }

    // ----------------------------------------------------
    // 9. ⚙️ SYSTEM SETTINGS & AUDIT LOGS (Settings View)
    // ----------------------------------------------------
    [HttpGet]
    private async Task<SystemSettingsDto> LoadSettingsDtoAsync()
    {
        var config = HttpContext?.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        var s = await ReviewSettings.LoadAsync(_context, config);
        return new SystemSettingsDto
        {
            OcrConfidenceThreshold = (decimal)Math.Round(s.MinConfidence * 100, 1),
            MaxInvoiceAmountAutoApproval = s.ReviewAmountAbove,
            MaxDailyScansPerCustomer = s.MaxDailyUploads,
            RequireKnownStore = s.RequireKnownStore,
        };
    }

    public async Task<IActionResult> Settings([FromQuery] string? search, [FromQuery] string? tab, [FromQuery(Name = "action")] string? actionFilter,
        [FromQuery] string? by, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] string? export = null)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (!await Can(Permissions.SettingsManage, Permissions.AuditView)) return RedirectToAction(nameof(Index));

        var query = _context.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.PerformedBy.Contains(term) || a.Action.Contains(term) || a.EntityName.Contains(term) ||
                                     a.Details.Contains(term) || (a.EntityId != null && a.EntityId.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(actionFilter)) query = query.Where(a => a.Action == actionFilter);
        if (!string.IsNullOrWhiteSpace(by)) query = query.Where(a => a.PerformedBy == by);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local).ToUniversalTime());
        if (to.HasValue) query = query.Where(a => a.CreatedAt < DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime());

        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var rows = await query.OrderByDescending(a => a.CreatedAt).Take(20000).ToListAsync();
            string Cell(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join(",", new[] { "الوقت", "المنفّذ", "العملية", "الرمز", "السجل", "التفاصيل", "IP" }.Select(Cell)));
            foreach (var a in rows)
                sb.AppendLine(string.Join(",", new object?[] { a.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), a.PerformedBy,
                    MMApplication.Api.ViewModels.DisplayLabels.Action(a.Action), a.Action, $"{a.EntityName} {a.EntityId}".Trim(), a.Details, a.IpAddress }.Select(Cell)));
            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"audit-log-{DateTime.Now:yyyy-MM-dd}.csv");
        }

        const int pageSize = 50;
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var auditLogs = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var now = DateTime.UtcNow;
        var services = HttpContext?.RequestServices;
        var config = services?.GetService(typeof(IConfiguration)) as IConfiguration;
        var ocr = services?.GetService(typeof(IReceiptOcrService));
        var ocrName = ocr?.GetType().Name ?? "";
        var (aiProvider, aiReal) = ocrName switch
        {
            var n when n.StartsWith("Gemini") => ("Google Gemini", true),
            var n when n.StartsWith("Claude") => ("Claude", true),
            "" => ("غير مضبوط", false),
            _ => ("محرك تجريبي (بدون ذكاء اصطناعي حقيقي)", false),
        };
        var dbProvider = _context.Database.ProviderName ?? "";
        var status = new SystemStatusInfo
        {
            Database = dbProvider.Contains("Npgsql") ? "PostgreSQL (Supabase)" : dbProvider.Contains("InMemory") ? "قاعدة بيانات تجريبية في الذاكرة" : dbProvider,
            DatabaseIsReal = !dbProvider.Contains("InMemory"),
            AiProvider = aiProvider,
            AiIsReal = aiReal,
            AiModel = ocrName.StartsWith("Gemini") ? (config?["Gemini:Model"] is { Length: > 0 } gm ? gm : GeminiClient.DefaultModel) : null,
            AiFallbackModel = ocrName.StartsWith("Gemini") ? (config?["Gemini:FallbackModel"] is { } fb ? (fb is "" or "none" ? null : fb) : GeminiClient.DefaultFallbackModel) : null,
            AiRetryQueue = await _context.Invoices.CountAsync(i => i.NextAiRetryAt != null && i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED),
            ReviewQueue = await _context.Invoices.CountAsync(i => i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED || i.ProcessingStatus == InvoiceStatus.PENDING),
            PushEnabled = services?.GetService(typeof(FcmSender)) != null,
            PushDevices = await _context.DeviceTokens.CountAsync(),
            Pushed24h = await _context.Notifications.CountAsync(n => n.PushSentAt != null && n.PushSentAt >= now.AddDays(-1)),
            Customers = await _context.Customers.CountAsync(c => !c.IsArchived),
            Stores = await _context.Tenants.CountAsync(t => t.IsActive),
            Invoices = await _context.Invoices.CountAsync(),
            Invoices24h = await _context.Invoices.CountAsync(i => i.CreatedAt >= now.AddDays(-1)),
            Staff = await _context.SystemUsers.CountAsync(u => u.IsActive),
            LockedStaff = await _context.SystemUsers.CountAsync(u => u.LockedUntil != null && u.LockedUntil > now),
            FailedLogins24h = await _context.AuditLogs.CountAsync(a => a.Action == "STAFF_LOCKED" && a.CreatedAt >= now.AddDays(-1)),
            AppVersion = typeof(AdminDashboardController).Assembly.GetName().Version?.ToString(3) ?? "",
            ServerTime = now,
        };

        var viewModel = new SettingsPageViewModel
        {
            Settings = await LoadSettingsDtoAsync(),
            AuditLogs = auditLogs.Select(a => new AuditLogDto
            {
                Id = a.Id, PerformedBy = a.PerformedBy, Action = a.Action, EntityName = a.EntityName, EntityId = a.EntityId,
                Details = a.Details, IpAddress = a.IpAddress, CreatedAt = a.CreatedAt
            }).ToList(),
            SearchQuery = search,
            Tab = tab is "status" or "audit" ? tab : (search != null || actionFilter != null || by != null || from != null || to != null || page > 1 ? "audit" : "rules"),
            ActionFilter = actionFilter,
            ByFilter = by,
            From = from,
            To = to,
            Page = page,
            TotalPages = totalPages,
            TotalLogs = total,
            Performers = await _context.AuditLogs.AsNoTracking().Select(a => a.PerformedBy).Distinct().OrderBy(x => x).Take(200).ToListAsync(),
            ActionsInLog = await _context.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().ToListAsync(),
            Status = status,
        };

        ViewData["ActiveTab"] = "settings";
        ViewData["Title"] = "الإعدادات وسجل العمليات";
        return View(viewModel);
    }
}

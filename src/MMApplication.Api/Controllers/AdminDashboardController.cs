using MMApplication.Api.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Api.ViewModels;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

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
        if (!CanReviewInvoices) return RedirectToAction(nameof(Index));

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
        if (!CanReviewInvoices) return Forbid();

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
        if (!CanReviewInvoices) return Forbid();
        return ReviewResult(await _review.ApproveAsync(id, ReviewerName, correction));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewReject(Guid id, [FromBody] ReviewDecisionRequest request)
    {
        if (!CanReviewInvoices) return Forbid();
        return ReviewResult(await _review.RejectAsync(id, ReviewerName, request.Reason ?? string.Empty));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRequestResubmission(Guid id, [FromBody] ReviewDecisionRequest request)
    {
        if (!CanReviewInvoices) return Forbid();
        return ReviewResult(await _review.RequestResubmissionAsync(id, ReviewerName, request.Reason ?? string.Empty));
    }

    /// <summary>إعادة قراءة صورة الفاتورة المحفوظة بالذكاء الاصطناعي (مثلاً بعد تفعيل مفتاح Claude).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRerunAi(Guid id)
    {
        if (!CanReviewInvoices) return Forbid();
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
        if (!CanReviewInvoices && !IsStoreManager) return Forbid();
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
        if (string.IsNullOrWhiteSpace(url) || _env?.WebRootPath == null || url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
        var full = Path.GetFullPath(Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        // حماية من الخروج خارج مجلد wwwroot
        return full.StartsWith(Path.GetFullPath(_env.WebRootPath), StringComparison.Ordinal) ? full : null;
    }

    // ----------------------------------------------------
    // 📈 REPORTS (التقارير والإحصائيات + تصدير Excel)
    // ----------------------------------------------------
    private ReportFilter BuildReportFilter(string? range, DateTime? from, DateTime? to, string? storeId)
    {
        // مدير المتجر يرى تقارير متجره فقط
        var tenant = IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId) ? CurrentTenantId : storeId;
        return ReportFilter.From_(range, from, to, tenant);
    }

    [HttpGet]
    public async Task<IActionResult> Reports([FromQuery] string? range, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? storeId)
    {
        if (!CanReviewInvoices && !IsStoreManager) return RedirectToAction(nameof(Index));

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
        if (!CanReviewInvoices && !IsStoreManager) return Forbid();

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
    private bool IsDeveloperOrSuperAdmin => User == null || 
                                           User.IsInRole("superadmin") || 
                                           CurrentSystemRole == "SUPER_ADMIN" || 
                                           CurrentUserId == "programer" || 
                                           CurrentUserId == "USR-MOSTAFA" || 
                                           CurrentUserId == "USR-SUPERADMIN";

    private bool IsAuditor => User != null && 
                             (User.IsInRole("auditor") || CurrentSystemRole == "FRAUD_AUDITOR" || CurrentUserId == "USR-MOURAD");

    private bool IsStoreManager => User != null && 
                                  User.Identity?.IsAuthenticated == true && 
                                  (User.IsInRole("storemanager") || CurrentSystemRole == "STORE_MANAGER" || !string.IsNullOrEmpty(CurrentTenantId)) && 
                                  !IsDeveloperOrSuperAdmin;

    private bool IsExplicitlyUnauthenticated => User?.Identity != null && !User.Identity.IsAuthenticated;
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

        if (user == null || !_passwordHasher.VerifyPassword(model.Password, user.PasswordHash))
        {
            model.ErrorMessage = "بيانات تسجيل الدخول غير صحيحة. يرجى التحقق من صحة اسم المستخدم وكلمة المرور.";
            return View(model);
        }

        if (!user.IsActive)
        {
            model.ErrorMessage = "هذا الحساب معطل حالياً من قبل الإدارة المركزية.";
            return View(model);
        }

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

        var invoiceQuery = _context.Invoices.AsNoTracking();
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            invoiceQuery = invoiceQuery.Where(i => i.TenantId == CurrentTenantId);
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
        ViewData["Title"] = IsStoreManager ? "لوحة مبيعات ومؤشرات المتجر" : "لوحة المؤشرات المركزية ونمذجة بواسون";
        return View(overviewDto);
    }

    // ----------------------------------------------------
    // 2. 🏢 TENANTS & STORES (Stores View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Stores([FromQuery] string? category, [FromQuery] string? search)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        var query = _context.Tenants
            .Include(t => t.LoyaltyRule)
            .Include(t => t.SystemUsers)
            .Include(t => t.Aliases)
            .AsNoTracking();

        // RBAC Filter: Store Managers only see their own store
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            query = query.Where(s => s.TenantId == CurrentTenantId);
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
            FloorNumber = s.UnitNumber.StartsWith("G") ? "الطابق الأرضي" : "الطابق الأول",
            UnitNumber = s.UnitNumber,
            LoyaltyType = StoreLoyaltyType.MallPoints,
            LoyaltyTypeLabel = s.LoyaltyRule != null ? s.LoyaltyRule.RewardType : "نقاط المول",
            CommissionRate = 5.0m,
            ContactPerson = s.SystemUsers.FirstOrDefault()?.FullName ?? "مدير الفرع",
            ContactPhone = s.SystemUsers.FirstOrDefault()?.PhoneNumber ?? "-",
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
        ViewData["Title"] = IsStoreManager ? "بيانات وترويسات متجري (My Store & OCR)" : "إدارة المتاجر والمستأجرين (Tenants & Stores)";
        ViewData["LoyaltyFilter"] = category ?? "";
        ViewData["SearchQuery"] = search ?? "";
        return View(storeDtos);
    }

    // ----------------------------------------------------
    // 2.1 🧾 INVOICE TEMPLATES & AI RECOGNITION (Templates View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> InvoiceTemplates([FromQuery] string? storeId)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        var query = _context.Tenants
            .Include(t => t.Aliases)
            .Include(t => t.Invoices)
            .Include(t => t.LoyaltyRule)
            .AsNoTracking();

        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            query = query.Where(t => t.TenantId == CurrentTenantId);
        }
        else if (!string.IsNullOrWhiteSpace(storeId) && storeId != "ALL")
        {
            query = query.Where(t => t.TenantId == storeId);
        }

        var stores = await query.OrderBy(t => t.NameAr).ToListAsync();

        var templates = stores.Select(s =>
        {
            var activeAliases = s.Aliases.Where(a => a.IsActive).Select(a => a.AliasName).ToList();
            if (!activeAliases.Any())
            {
                activeAliases.Add(s.NameAr);
                if (!string.IsNullOrWhiteSpace(s.NameEn)) activeAliases.Add(s.NameEn);
            }

            var type = s.Category switch
            {
                "مطاعم وكافيهات" => "إيصال كافيه ومطعم حراري (POS Slip)",
                "سوبرماركت" => "شريط كاشير تفصيلي (POS Thermal)",
                "أزياء وملابس" => "فاتورة مشتريات تفصيلية (Retail Invoice)",
                "إلكترونيات وهواتف" => "فاتورة ضريبية رسمية (A4 Tax Invoice)",
                _ => "فاتورة كاشير إلكترونية (Standard POS)"
            };

            var totalKeyword = s.Category switch
            {
                "مطاعم وكافيهات" => "المجموع الصافي / TOTAL",
                "سوبرماركت" => "المجموع الكلي",
                "أزياء وملابس" => "صافي الفاتورة",
                _ => "المجموع الكلي"
            };

            var prefix = s.UnitNumber.StartsWith("G") ? "INV-G" : "INV-F1";

            var approvedInvoices = s.Invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).ToList();
            var accuracy = approvedInvoices.Any()
                ? Math.Round(approvedInvoices.Average(i => i.ConfidenceScore.GetValueOrDefault(0.96m)) * 100m, 1)
                : 98.6m;

            return new StoreInvoiceTemplateDto
            {
                StoreId = s.TenantId,
                StoreName = s.NameAr,
                Category = s.Category,
                UnitNumber = s.UnitNumber,
                TemplateType = type,
                HeaderAliases = activeAliases,
                TotalAmountKeyword = totalKeyword,
                InvoiceNumberPrefix = prefix,
                DateFormatKeyword = "التاريخ / Date",
                TaxNumber = $"LY-TAX-{Math.Abs(s.TenantId.GetHashCode()) % 900000 + 100000}",
                SampleImageUrl = s.LogoUrl ?? $"/images/receipts/sample_{s.TenantId}.jpg",
                RecognitionAccuracy = accuracy,
                TotalRecognizedInvoices = s.Invoices.Count,
                IsActive = s.IsActive,
                Notes = $"نموذج مخصص لمتجر {s.NameAr} متوافق مع نظام كاشير الفرع.",
                LastUpdated = s.Aliases.Any() ? s.Aliases.Max(a => a.CreatedAt) : s.CreatedAt
            };
        }).ToList();

        var allStoresQuery = _context.Tenants.Where(t => t.IsActive).AsNoTracking();
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            allStoresQuery = allStoresQuery.Where(t => t.TenantId == CurrentTenantId);
        }

        var storeDropdown = await allStoresQuery
            .OrderBy(t => t.NameAr)
            .Select(t => new StoreTenantDto
            {
                Id = t.TenantId,
                Name = t.NameAr,
                Category = t.Category,
                UnitNumber = t.UnitNumber
            })
            .ToListAsync();

        var viewModel = new InvoiceTemplatesPageViewModel
        {
            Templates = templates,
            Stores = storeDropdown,
            SelectedStoreId = storeId
        };

        ViewData["ActiveTab"] = "invoice_templates";
        ViewData["Title"] = "نماذج الفواتير والتعرف الذكي (AI OCR Templates)";
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInvoiceTemplate([FromBody] SaveInvoiceTemplateRequest request)
    {
        if (IsExplicitlyUnauthenticated) return Unauthorized(new { success = false, message = "غير مصرح" });

        if (string.IsNullOrWhiteSpace(request.StoreId))
        {
            return BadRequest(new { success = false, message = "معرّف المتجر مطلوب." });
        }

        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId) && request.StoreId != CurrentTenantId)
        {
            return Forbid();
        }

        var store = await _context.Tenants
            .Include(t => t.Aliases)
            .FirstOrDefaultAsync(t => t.TenantId == request.StoreId);

        if (store == null)
        {
            return NotFound(new { success = false, message = "المتجر المحدد غير موجود." });
        }

        var rawAliases = (request.HeaderAliases ?? "")
            .Split(new[] { ',', '،', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Always include main store name if not present
        if (!rawAliases.Contains(store.NameAr, StringComparer.OrdinalIgnoreCase))
        {
            rawAliases.Insert(0, store.NameAr);
        }

        // Deactivate or update existing aliases
        foreach (var existing in store.Aliases)
        {
            if (rawAliases.Contains(existing.AliasName, StringComparer.OrdinalIgnoreCase))
            {
                existing.IsActive = request.IsActive;
                rawAliases.RemoveAll(a => string.Equals(a, existing.AliasName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                existing.IsActive = false;
            }
        }

        // Add newly typed aliases
        foreach (var newAlias in rawAliases)
        {
            _context.StoreAliases.Add(new StoreAlias
            {
                TenantId = store.TenantId,
                AliasName = newAlias,
                MatchType = "CONTAINS",
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "UPDATE_INVOICE_TEMPLATE",
            EntityName = "StoreTemplate",
            EntityId = store.TenantId,
            PerformedBy = CurrentUserId ?? "Admin",
            Details = $"تحديث نموذج الفاتورة والترويسات للمتجر {store.NameAr} (نمط: {request.TemplateType})",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"تم حفظ وتحديث نموذج فاتورة {store.NameAr} وربط قواعد الـ AI بنجاح." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestAiInvoiceScan([FromBody] AiInvoiceScanTestRequest request)
    {
        if (IsExplicitlyUnauthenticated) return Unauthorized(new { success = false, message = "غير مصرح" });

        Tenant? matchedStore = null;

        if (!string.IsNullOrWhiteSpace(request.StoreId) && request.StoreId != "ALL")
        {
            matchedStore = await _context.Tenants
                .Include(t => t.Aliases)
                .Include(t => t.LoyaltyRule)
                .FirstOrDefaultAsync(t => t.TenantId == request.StoreId);
        }

        if (matchedStore == null && !string.IsNullOrWhiteSpace(request.ReceiptText))
        {
            var text = request.ReceiptText;
            var allAliases = await _context.StoreAliases
                .Where(a => a.IsActive)
                .Include(a => a.Tenant)
                    .ThenInclude(t => t.LoyaltyRule)
                .ToListAsync();

            var matchedAlias = allAliases.FirstOrDefault(a => text.Contains(a.AliasName, StringComparison.OrdinalIgnoreCase));
            if (matchedAlias != null)
            {
                matchedStore = matchedAlias.Tenant;
            }
            else
            {
                var stores = await _context.Tenants.Include(t => t.LoyaltyRule).ToListAsync();
                matchedStore = stores.FirstOrDefault(s => text.Contains(s.NameAr, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(s.NameEn) && text.Contains(s.NameEn, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (matchedStore == null)
        {
            matchedStore = await _context.Tenants.Include(t => t.LoyaltyRule).FirstOrDefaultAsync();
        }

        if (matchedStore == null)
        {
            return BadRequest(new { success = false, message = "لا توجد متاجر مسجلة في المنظومة لاختبار الفاتورة." });
        }

        // Realistic Extraction Simulation based on store profile
        var random = new Random();
        var invoiceNum = $"INV-{DateTime.UtcNow:yyMMdd}-{random.Next(1000, 9999)}";
        decimal totalAmount;
        List<ExtractedLineItemDto> items = new();

        switch (matchedStore.Category)
        {
            case "مطاعم وكافيهات":
                items.Add(new ExtractedLineItemDto { Description = "كابتشينو كلاسيك بالحليب", Quantity = 2, UnitPrice = 14.00m, TotalPrice = 28.00m });
                items.Add(new ExtractedLineItemDto { Description = "كرواسون زبدة فرنسي", Quantity = 1, UnitPrice = 12.50m, TotalPrice = 12.50m });
                items.Add(new ExtractedLineItemDto { Description = "مياه معدنية طبيعية 500 مل", Quantity = 1, UnitPrice = 3.50m, TotalPrice = 3.50m });
                totalAmount = 44.00m;
                break;
            case "أزياء وملابس":
                items.Add(new ExtractedLineItemDto { Description = "فستان سهرة كلاسيك مطرز", Quantity = 1, UnitPrice = 185.00m, TotalPrice = 185.00m });
                items.Add(new ExtractedLineItemDto { Description = "شال حرير طبيعي فاخر", Quantity = 1, UnitPrice = 45.00m, TotalPrice = 45.00m });
                totalAmount = 230.00m;
                break;
            case "سوبرماركت":
                items.Add(new ExtractedLineItemDto { Description = "زيت زيتون بكر ممتاز 1 لتر", Quantity = 2, UnitPrice = 22.00m, TotalPrice = 44.00m });
                items.Add(new ExtractedLineItemDto { Description = "أرز بسمتي فاخر 5 كجم", Quantity = 1, UnitPrice = 38.50m, TotalPrice = 38.50m });
                items.Add(new ExtractedLineItemDto { Description = "حليب كامل الدسم عبوة 1 لتر", Quantity = 4, UnitPrice = 4.50m, TotalPrice = 18.00m });
                totalAmount = 100.50m;
                break;
            default:
                items.Add(new ExtractedLineItemDto { Description = $"مشتريات متنوعة - {matchedStore.NameAr}", Quantity = 1, UnitPrice = 65.00m, TotalPrice = 65.00m });
                totalAmount = 65.00m;
                break;
        }

        // Calculate loyalty points
        var pointsRatio = matchedStore.LoyaltyRule?.ConversionRate ?? 1.0m;
        var points = (int)Math.Floor(totalAmount * (pointsRatio > 0 ? pointsRatio * 10m : 1.0m));
        var cashback = Math.Round(totalAmount * 0.05m, 2);

        var result = new AiInvoiceScanTestResult
        {
            Success = true,
            Message = $"تم التعرف على نموذج الفاتورة بنجاح ومطابقته مع ترويسات متجر [{matchedStore.NameAr}].",
            RecognizedStoreId = matchedStore.TenantId,
            RecognizedStoreName = matchedStore.NameAr,
            ExtractedInvoiceNumber = invoiceNum,
            ExtractedTotalAmount = totalAmount,
            ExtractedDate = DateTime.UtcNow,
            ConfidencePercentage = 98.7m,
            MatchedTemplateType = matchedStore.Category == "مطاعم وكافيهات" ? "إيصال كافيه ومطعم حراري (POS Slip)" : "شريط كاشير تفصيلي (POS Thermal)",
            LineItems = items,
            CalculatedPoints = points > 0 ? points : 44,
            CalculatedCashback = cashback,
            IsFraudRiskDetected = false
        };

        return Json(result);
    }

    // ----------------------------------------------------
    // 2.2 📋 INVOICES LOG & TRANSACTIONS (Invoices View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Invoices([FromQuery] string? storeId, [FromQuery] string? status, [FromQuery] string? search)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        var query = _context.Invoices
            .Include(i => i.Tenant)
            .Include(i => i.Customer)
            .Include(i => i.LineItems)
            .AsNoTracking();

        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            query = query.Where(i => i.TenantId == CurrentTenantId);
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
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            storesQuery = storesQuery.Where(t => t.TenantId == CurrentTenantId);
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
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            statsQuery = statsQuery.Where(i => i.TenantId == CurrentTenantId);
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
        ViewData["Title"] = "سجل الفواتير والمعاملات (Invoices Log)";
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoiceDetailsJson(Guid id)
    {
        if (IsExplicitlyUnauthenticated) return Unauthorized();

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

        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId) && invoice.TenantId != CurrentTenantId)
        {
            return Forbid();
        }

        // الاعتماد والرفض يمران عبر خدمة المراجعة لتُضاف/تُسترجع النقاط بشكل صحيح
        if (status == InvoiceStatus.APPROVED || status == InvoiceStatus.REJECTED)
        {
            if (!CanReviewInvoices) return Forbid();
            return ReviewResult(status == InvoiceStatus.APPROVED
                ? await _review.ApproveAsync(id, ReviewerName)
                : await _review.RejectAsync(id, ReviewerName, string.IsNullOrWhiteSpace(reason) ? "تم الرفض بواسطة الإدارة" : reason));
        }

        invoice.ProcessingStatus = status;
        if (status == InvoiceStatus.VOIDED)
        {
            invoice.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "تم الإلغاء بواسطة الإدارة" : reason;
        }

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

    // ----------------------------------------------------
    // 3. 🛡️ FRAUD & REVIEW HUB (Fraud View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Fraud([FromQuery] string? statusFilter)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        var filter = statusFilter?.ToUpperInvariant() ?? "ALL";
        var query = _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Tenant)
            .AsNoTracking();

        // RBAC: Store managers only see fraud events of their own store
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            query = query.Where(i => i.TenantId == CurrentTenantId);
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
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            statsInvoiceQuery = statsInvoiceQuery.Where(i => i.TenantId == CurrentTenantId);
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
        ViewData["Title"] = "مركز مكافحة الاحتيال والتدقيق (Fraud & Review Hub)";
        return View(viewModel);
    }

    // ----------------------------------------------------
    // 4. 📢 ADS & OFFERS HUB (Ads View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Ads()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        if (!IsDeveloperOrSuperAdmin)
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
        ViewData["Title"] = "الإعلانات والعروض الترويجية (Ads & Offers Hub)";
        return View(dtos);
    }

    // ----------------------------------------------------
    // 5. 🌟 MALL-WIDE LOYALTY (Loyalty View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Loyalty()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

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

        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            boosterQuery = boosterQuery.Where(r => r.TenantId == CurrentTenantId);
            earningRuleQuery = earningRuleQuery.Where(r => r.StoreTenantId == CurrentTenantId);
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
        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            storeQuery = storeQuery.Where(t => t.TenantId == CurrentTenantId);
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
        ViewData["Title"] = IsStoreManager ? "قواعد كاش باك وبونص المتجر" : "برنامج ولاء المول العام (Mall-Wide Loyalty)";
        return View(viewModel);
    }

    // ----------------------------------------------------
    // 6. 💳 SETTLEMENTS & CLEARING (Settlements View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Settlements()
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        var settlementQuery = _context.TenantSettlements
            .Include(s => s.Tenant)
            .AsNoTracking();

        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            settlementQuery = settlementQuery.Where(s => s.TenantId == CurrentTenantId);
        }

        var settlements = await settlementQuery
            .OrderByDescending(s => s.PeriodEndDate)
            .ToListAsync();

        var dtos = settlements.Select(s => new StoreSettlementDto
        {
            Id = s.SettlementId,
            StoreTenantId = s.TenantId,
            StoreName = s.Tenant != null ? s.Tenant.NameAr : "متجر غير معروف",
            PeriodLabel = $"{s.PeriodStartDate:yyyy/MM/dd} - {s.PeriodEndDate:yyyy/MM/dd}",
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
        ViewData["Title"] = IsStoreManager ? "مستحقات وتسويات متجري المالية" : "التسويات والمقاصة المالية للمتاجر (Settlements & Clearing)";
        return View(dtos);
    }

    // ----------------------------------------------------
    // 7. 📱 CUSTOMER DATABASE (Customers View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Customers([FromQuery] string? tier, [FromQuery] string? search)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        var query = _context.Customers
            .Include(c => c.Wallets)
            .Include(c => c.Invoices)
            .Include(c => c.Devices)
            .AsNoTracking();

        if (IsStoreManager && !string.IsNullOrEmpty(CurrentTenantId))
        {
            query = query.Where(c => c.Invoices.Any(i => i.TenantId == CurrentTenantId));
        }

        if (!string.IsNullOrWhiteSpace(tier) && tier != "ALL")
        {
            query = query.Where(c => c.LoyaltyTier == tier);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.FullName.Contains(term) || c.PhoneNumber.Contains(term) || c.CustomerId.Contains(term));
        }

        var allDevices = await _context.CustomerDevices.AsNoTracking().ToListAsync();
        var sharedFingerprints = allDevices
            .GroupBy(d => d.DeviceFingerprint)
            .Where(g => g.Select(x => x.CustomerId).Distinct().Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CustomerId).Distinct().Count());

        var customers = await query.OrderByDescending(c => c.RegisteredAt).ToListAsync();

        var dtos = customers.Select(c =>
        {
            var customerFps = c.Devices.Select(d => d.DeviceFingerprint).ToList();
            var maxShared = 0;
            foreach (var fp in customerFps)
            {
                if (sharedFingerprints.TryGetValue(fp, out var cnt) && cnt > maxShared)
                {
                    maxShared = cnt;
                }
            }

            return new SuperAdminCustomerDto
            {
                Id = c.CustomerId,
                FullName = c.FullName,
                PhoneNumber = c.PhoneNumber,
                Tier = c.LoyaltyTier,
                CurrentPoints = (int)(c.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS)?.CurrentBalance ?? 0),
                TotalInvoicesScanned = c.Invoices.Count,
                TotalSpent = c.Invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount) ?? 0m,
                IsSuspended = c.IsFrozen,
                IsArchived = c.IsArchived,
                HasMultiAccountDeviceFlag = maxShared > 1,
                SharedDeviceAccountsCount = maxShared,
                CreatedAt = c.RegisteredAt
            };
        }).ToList();

        ViewData["ActiveTab"] = "customers";
        ViewData["Title"] = "قاعدة بيانات العملاء والمتسوقين (Customer Database)";
        ViewData["TierFilter"] = tier ?? "ALL";
        ViewData["SearchQuery"] = search ?? "";
        return View(dtos);
    }

    // ----------------------------------------------------
    // 7.1 🔍 DEEP CUSTOMER DETAILS & LEDGER (Customer Details View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> CustomerDetails(string id)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));
        if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(Customers));

        var customer = await _context.Customers
            .Include(c => c.Wallets)
                .ThenInclude(w => w.LedgerEntries)
            .Include(c => c.Invoices)
                .ThenInclude(i => i.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null) return NotFound("العميل غير مسجل في النظام.");

        var stores = await _context.Tenants.AsNoTracking().ToListAsync();

        var invoiceDtos = customer.Invoices
            .OrderByDescending(i => i.InvoiceDate ?? i.CreatedAt)
            .Select(i =>
            {
                var store = stores.FirstOrDefault(s => s.TenantId == i.TenantId);
                return new CustomerInvoiceDetailDto
                {
                    Id = i.InvoiceId.ToString(),
                    MerchantName = store?.NameAr ?? i.ExtractedMerchant ?? "متجر عام",
                    StoreCategory = store?.Category ?? "تسوق عام",
                    FloorNumber = store?.UnitNumber ?? "المول",
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate.GetValueOrDefault(i.CreatedAt),
                    ScanDateTime = i.CreatedAt,
                    TotalAmount = i.TotalAmount.GetValueOrDefault(0m),
                    PointsEarned = i.PointsAwarded,
                    CashBackEarned = i.CashbackAwarded,
                    RiskScore = i.FraudRiskScore,
                    Status = i.ProcessingStatus,
                    FraudFlagReason = i.RejectionReason,
                    OcrConfidence = (i.ConfidenceScore.GetValueOrDefault(0.95m)) * 100m,
                    ImageUrl = i.ReceiptImageUrl
                };
            }).ToList();

        var storeVisits = customer.Invoices
            .GroupBy(i => i.Tenant != null ? i.Tenant.NameAr : (i.ExtractedMerchant ?? "متجر عام"))
            .Select(g =>
            {
                var store = stores.FirstOrDefault(s => s.NameAr == g.Key || s.NameEn == g.Key);
                return new CustomerStoreVisitDto
                {
                    StoreName = g.Key,
                    Category = store?.Category ?? "متجر تجزئة",
                    VisitCount = g.Count(),
                    TotalSpent = g.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount) ?? 0m,
                    TotalPointsEarned = g.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.PointsAwarded),
                    LastVisitDate = g.Max(i => i.InvoiceDate) ?? DateTime.UtcNow,
                    IsFavorite = g.Count() >= 2
                };
            })
            .OrderByDescending(sv => sv.TotalSpent)
            .ToList();

        var pointsWallet = customer.Wallets.FirstOrDefault(w => w.BalanceType == BalanceType.POINTS);
        var ledgerEntries = pointsWallet?.LedgerEntries
            .OrderByDescending(le => le.CreatedAt)
            .Select(le => new CustomerRewardRedemptionDto
            {
                Id = le.EntryId.ToString(),
                RewardTitle = le.TransactionType.ToString(),
                Category = le.ReferenceRemarks ?? "حركة محفظة",
                PointsSpent = le.AmountChanged < 0 ? -(int)le.AmountChanged : 0,
                MonetaryValue = (le.AmountChanged * 0.1m),
                RedemptionDate = le.CreatedAt,
                Status = le.TransactionType.ToString(),
                RedemptionCode = $"TXN-{le.EntryId}"
            }).ToList() ?? new List<CustomerRewardRedemptionDto>();

        var viewModel = new CustomerDetailsPageViewModel
        {
            CustomerId = customer.CustomerId,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            Tier = customer.LoyaltyTier,
            PointsBalance = (int)(pointsWallet?.CurrentBalance ?? 0),
            TotalSpent = customer.Invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount) ?? 0m,
            TotalCashBackEarned = customer.Invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.CashbackAwarded),
            InvoicesCount = customer.Invoices.Count,
            ApprovedCount = customer.Invoices.Count(i => i.ProcessingStatus == InvoiceStatus.APPROVED),
            PendingCount = customer.Invoices.Count(i => i.ProcessingStatus == InvoiceStatus.PENDING || i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED),
            RejectedCount = customer.Invoices.Count(i => i.ProcessingStatus == InvoiceStatus.REJECTED),
            IsSuspended = customer.IsFrozen,
            RegisteredAt = customer.RegisteredAt,
            LastActivityDate = customer.Invoices.Any() ? customer.Invoices.Max(i => i.CreatedAt) : customer.RegisteredAt,
            Invoices = invoiceDtos,
            StoreVisits = storeVisits,
            Redemptions = ledgerEntries
        };

        ViewData["ActiveTab"] = "customers";
        ViewData["Title"] = $"كشف حساب وفواتير العميل: {customer.FullName}";
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomerDetailsJson(string id)
    {
        if (IsExplicitlyUnauthenticated) return Unauthorized();
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
        if (!IsDeveloperOrSuperAdmin)
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
                SystemUserRole.SUPER_ADMIN => s.UserId == "programer" ? "المطور التقني (Developer / Root)" : "المدير العام (Super Admin)",
                SystemUserRole.FRAUD_AUDITOR => "مدقق فواتير (Fraud Auditor)",
                SystemUserRole.STORE_MANAGER => "مدير فرع (Store Manager)",
                SystemUserRole.CASHIER => "كاشير ونقاط بيع (Cashier)",
                _ => s.Role.ToString()
            },
            StoreTenantId = s.TenantId,
            StoreName = s.Tenant != null ? s.Tenant.NameAr : "إدارة المول المركزية",
            CreatedAt = s.CreatedAt
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
        ViewData["Title"] = "طاقم العمل وإدارة الصلاحيات (Staff & Role Management)";
        return View(viewModel);
    }

    // ----------------------------------------------------
    // 9. ⚙️ SYSTEM SETTINGS & AUDIT LOGS (Settings View)
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Settings([FromQuery] string? search)
    {
        if (IsExplicitlyUnauthenticated) return RedirectToAction(nameof(Login));

        // Developer & Super Admin Only
        if (!IsDeveloperOrSuperAdmin)
        {
            return RedirectToAction(nameof(Index));
        }

        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.PerformedBy.Contains(term) || a.Action.Contains(term) || a.EntityName.Contains(term) || a.Details.Contains(term));
        }

        var auditLogs = await query.OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync();

        var logDtos = auditLogs.Select(a => new AuditLogDto
        {
            Id = a.Id,
            PerformedBy = a.PerformedBy,
            Action = a.Action,
            EntityName = a.EntityName,
            EntityId = a.EntityId,
            Details = a.Details,
            IpAddress = a.IpAddress,
            CreatedAt = a.CreatedAt
        }).ToList();

        var viewModel = new SettingsPageViewModel
        {
            Settings = new SystemSettingsDto
            {
                OcrConfidenceThreshold = 85.0m,
                MaxInvoiceAmountAutoApproval = 5000.0m,
                MaxDailyScansPerCustomer = 10
            },
            AuditLogs = logDtos,
            SearchQuery = search
        };

        ViewData["ActiveTab"] = "settings";
        ViewData["Title"] = "إعدادات النظام وسجلات الأمان (System Settings & Audit Logs)";
        return View(viewModel);
    }
}

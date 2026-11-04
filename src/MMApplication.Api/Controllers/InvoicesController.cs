using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Api.DTOs;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IReceiptOcrService _ocrService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFlexibleLoyaltyEvaluationService _loyaltyEvaluationService;
    private readonly ILogger<InvoicesController> _logger;
    private readonly IConfiguration? _configuration;
    private readonly IHostEnvironment? _environment;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];

    public InvoicesController(
        AppDbContext context,
        IReceiptOcrService ocrService,
        IFileStorageService fileStorageService,
        IFlexibleLoyaltyEvaluationService loyaltyEvaluationService,
        ILogger<InvoicesController> logger,
        IConfiguration? configuration = null,
        IHostEnvironment? environment = null)
    {
        _context = context;
        _ocrService = ocrService;
        _fileStorageService = fileStorageService;
        _loyaltyEvaluationService = loyaltyEvaluationService;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>
    /// قواعد التحويل لمراجعة الموظفين بدل القبول التلقائي. القائمة الفارغة = قبول تلقائي.
    /// الحدود قابلة للتعديل من appsettings تحت القسم "InvoiceReview".
    /// </summary>
    private async Task<List<string>> CollectReviewReasonsAsync(
        ExtractedReceiptDto extracted, string? tenantId, DateTime invoiceDate, decimal totalAmount, int fraudRiskScore)
    {
        var reasons = new List<string>();
        var minConfidence = _configuration?.GetValue<double?>("InvoiceReview:MinAutoApproveConfidence") ?? 0.85;
        var amountThreshold = _configuration?.GetValue<decimal?>("InvoiceReview:ReviewAmountAbove") ?? 3000m;

        if (extracted.RequiresManualReview)
        {
            reasons.Add("تعذّرت القراءة الآلية للفاتورة");
            reasons.AddRange(extracted.AuthenticityConcerns);
            return reasons;
        }

        if (fraudRiskScore >= 35) reasons.Add("الجهاز مستخدم من حسابات عملاء أخرى");
        if (extracted.Confidence < minConfidence) reasons.Add($"ثقة القراءة منخفضة ({extracted.Confidence:P0})");
        reasons.AddRange(extracted.AuthenticityConcerns.Select(c => $"ملاحظة الذكاء الاصطناعي: {c}"));
        var requireKnownStore = _configuration?.GetValue<bool?>("InvoiceReview:RequireKnownStore") ?? true;
        if (requireKnownStore && string.IsNullOrEmpty(tenantId)) reasons.Add("المتجر غير معروف ضمن متاجر المول");
        if (totalAmount > amountThreshold) reasons.Add($"مبلغ مرتفع يتجاوز {amountThreshold:N0} د.ل");
        if (!string.IsNullOrEmpty(extracted.Currency) &&
            !extracted.Currency.Equals("LYD", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"عملة غير الدينار الليبي ({extracted.Currency})");
        }

        if (extracted.InvoiceDate.HasValue)
        {
            var maxAgeDays = 7;
            if (!string.IsNullOrEmpty(tenantId))
            {
                var rule = await _context.TenantLoyaltyRules.AsNoTracking().FirstOrDefaultAsync(r => r.TenantId == tenantId);
                if (rule != null && rule.MaxInvoiceAgeDays > 0) maxAgeDays = rule.MaxInvoiceAgeDays;
            }
            if (invoiceDate.Date < DateTime.UtcNow.Date.AddDays(-maxAgeDays)) reasons.Add($"الفاتورة أقدم من {maxAgeDays} أيام");
            if (invoiceDate.Date > DateTime.UtcNow.Date.AddDays(1)) reasons.Add("تاريخ الفاتورة في المستقبل");
        }

        // المحرك الوهمي مسموح بالقبول التلقائي في بيئة التطوير والاختبار فقط
        var isDevOrTest = _environment == null || _environment.IsDevelopment() || _environment.IsEnvironment("Testing");
        if (extracted.Provider == "Mock" && !isDevOrTest) reasons.Add("القراءة الآلية بالذكاء الاصطناعي غير مفعّلة");

        return reasons;
    }

    /// <summary>
    /// رفع صورة الفاتورة واستخراج البيانات واحتساب وإضافة نقاط الولاء لمحفظة العميل.
    /// (يتطلب مصادقة JWT - يتم استخراج معرف العميل تلقائياً من التوكن إن لم يحدد).
    /// </summary>
    [HttpPost("upload")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(InvoiceUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadInvoice([FromForm] InvoiceUploadRequest request)
    {
        var loggedInUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        var userRole = User?.FindFirst(ClaimTypes.Role)?.Value;

        var targetCustomerId = (!string.IsNullOrWhiteSpace(request.CustomerId) && userRole == "SUPER_ADMIN")
            ? request.CustomerId.Trim()
            : (!string.IsNullOrWhiteSpace(request.CustomerId) ? request.CustomerId.Trim() : loggedInUserId);

        if (string.IsNullOrWhiteSpace(targetCustomerId))
        {
            return BadRequest(new { success = false, message = "لم يتم التعرف على معرف العميل (CustomerId is missing)." });
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { success = false, message = "يرجى رفع ملف صالح لصورة الفاتورة (File is empty or missing)." });
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                success = false,
                message = $"نوع الملف غير مدعوم ({extension}). الأنواع المدعومة هي: {string.Join(", ", AllowedExtensions)}"
            });
        }

        var customer = await _context.Customers
            .Include(c => c.Wallets)
            .FirstOrDefaultAsync(c => c.CustomerId == targetCustomerId);

        if (customer == null)
        {
            return NotFound(new
            {
                success = false,
                message = $"العميل بالمعرف '{targetCustomerId}' غير موجود في النظام (Customer not found)."
            });
        }

        if (customer.IsArchived || customer.IsFrozen)
        {
            return BadRequest(new
            {
                success = false,
                message = "حساب العميل موقوف أو مؤرشف، يرجى مراجعة إدارة المول (Customer account is suspended or archived)."
            });
        }

        // --- 1. Device Tracking & Anti-Sybil Check ---
        var deviceFp = request.DeviceFingerprint
            ?? Request?.Headers?["X-Device-Fingerprint"].FirstOrDefault()
            ?? Request?.Headers?["User-Agent"].FirstOrDefault();

        var deviceModel = request.DeviceModel
            ?? Request?.Headers?["X-Device-Model"].FirstOrDefault()
            ?? "Mobile Device";

        var clientIp = HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var fraudRiskScore = 0;

        if (!string.IsNullOrWhiteSpace(deviceFp))
        {
            var existingDev = await _context.CustomerDevices
                .FirstOrDefaultAsync(d => d.CustomerId == customer.CustomerId && d.DeviceFingerprint == deviceFp);

            if (existingDev == null)
            {
                await _context.CustomerDevices.AddAsync(new CustomerDevice
                {
                    CustomerId = customer.CustomerId,
                    DeviceFingerprint = deviceFp,
                    DeviceModel = deviceModel,
                    IpAddress = clientIp,
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                });
            }
            else
            {
                existingDev.LastSeenAt = DateTime.UtcNow;
                existingDev.IpAddress = clientIp;
            }

            var otherAccountsCount = await _context.CustomerDevices
                .Where(d => d.DeviceFingerprint == deviceFp && d.CustomerId != customer.CustomerId)
                .Select(d => d.CustomerId)
                .Distinct()
                .CountAsync();

            if (otherAccountsCount > 0)
            {
                fraudRiskScore += 40; // Anti-sybil flag: shared physical device!
            }
        }

        // قائمة متاجر المول وأسمائها البديلة تُرسل لمحرك القراءة ليطابق المتجر بدقة
        var ocrContext = await MMApplication.Api.Services.ReceiptContextBuilder.BuildAsync(_context, request.File.ContentType, request.File.FileName);

        ExtractedReceiptDto extractedData;
        using (var stream = request.File.OpenReadStream())
        {
            extractedData = await _ocrService.ExtractReceiptDataAsync(stream, ocrContext);
        }

        if (extractedData == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "فشل التعرف على بيانات الفاتورة (Failed to extract receipt data)."
            });
        }

        if (!extractedData.IsReceipt && !extractedData.RequiresManualReview)
        {
            return BadRequest(new
            {
                success = false,
                message = "الصورة المرفوعة لا تبدو فاتورة شراء. يرجى تصوير فاتورة المتجر كاملة وبوضوح."
            });
        }

        // عند تعذّر القراءة الآلية (خدمة متوقفة مثلاً) لا نرفض الفاتورة: تُحفظ وتُحوّل لمراجعة الموظفين
        var needsManualRead = extractedData.RequiresManualReview;

        if (!needsManualRead && (
            string.IsNullOrWhiteSpace(extractedData.MerchantName) ||
            string.IsNullOrWhiteSpace(extractedData.InvoiceNumber) ||
            !extractedData.TotalAmount.HasValue ||
            extractedData.TotalAmount.Value <= 0))
        {
            return BadRequest(new
            {
                success = false,
                message = "تعذر استخراج بعض الحقول الأساسية من الفاتورة (اسم المتجر، رقم الفاتورة، أو المبلغ الإجمالي).",
                extractedData
            });
        }

        var merchantName = extractedData.MerchantName?.Trim() ?? "بانتظار القراءة اليدوية";
        var invoiceNumber = extractedData.InvoiceNumber?.Trim() ?? string.Empty;
        var totalAmount = extractedData.TotalAmount ?? 0m;
        var invoiceDate = extractedData.InvoiceDate ?? DateTime.UtcNow;

        // --- 2. Store Alias & Header Matcher Engine ---
        // أولاً: المتجر الذي طابقه الذكاء الاصطناعي من قائمة متاجر المول
        Tenant? matchedTenant = null;
        if (!string.IsNullOrEmpty(extractedData.MatchedTenantId))
        {
            matchedTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.TenantId == extractedData.MatchedTenantId);
        }

        matchedTenant ??= needsManualRead ? null : await _context.Tenants
            .FirstOrDefaultAsync(t => t.NameAr.Contains(merchantName) || t.NameEn.Contains(merchantName) || merchantName.Contains(t.NameAr) || merchantName.Contains(t.NameEn));

        var tenantId = matchedTenant?.TenantId;

        if (string.IsNullOrEmpty(tenantId) && !needsManualRead)
        {
            var activeAliases = await _context.StoreAliases
                .Where(a => a.IsActive)
                .Include(a => a.Tenant)
                .ToListAsync();

            var matchedAlias = activeAliases.FirstOrDefault(a =>
                a.MatchType == "EXACT"
                    ? string.Equals(a.AliasName.Trim(), merchantName, StringComparison.OrdinalIgnoreCase)
                    : merchantName.Contains(a.AliasName.Trim(), StringComparison.OrdinalIgnoreCase) || a.AliasName.Trim().Contains(merchantName, StringComparison.OrdinalIgnoreCase)
            );

            if (matchedAlias != null)
            {
                tenantId = matchedAlias.TenantId;
                matchedTenant = matchedAlias.Tenant;
            }
        }

        // Deduplication check (including VOIDED/REJECTED invoices to block reuse!)
        var isDuplicate = !string.IsNullOrEmpty(invoiceNumber) && await _context.Invoices.AnyAsync(i =>
            (i.TenantId == tenantId || i.ExtractedMerchant == merchantName) && i.InvoiceNumber == invoiceNumber);

        if (isDuplicate)
        {
            return Conflict(new
            {
                success = false,
                message = $"هذه الفاتورة مسجلة مسبقاً في النظام لنفس المتجر ({merchantName}) ورقم الفاتورة ({invoiceNumber}). لا يمكن استخدام الفاتورة أكثر من مرة.",
                merchantName,
                invoiceNumber
            });
        }

        // --- 3. Flexible Loyalty Earning & Booster Evaluation Engine ---
        var evalResult = await _loyaltyEvaluationService.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = totalAmount,
            TenantId = tenantId,
            MerchantName = merchantName,
            CustomerTier = customer.LoyaltyTier ?? "BRONZE",
            Items = extractedData.Items,
            InvoiceDate = invoiceDate
        });

        var pointsEarned = evalResult.TotalPointsAwarded;

        string imageUrl;
        using (var uploadStream = request.File.OpenReadStream())
        {
            imageUrl = await _fileStorageService.SaveFileAsync(uploadStream, request.File.FileName, "invoices");
        }

        // --- 4. قرار القبول التلقائي أو التحويل لمراجعة الموظفين ---
        var reviewReasons = await CollectReviewReasonsAsync(extractedData, tenantId, invoiceDate, totalAmount, fraudRiskScore);
        var initialStatus = reviewReasons.Count > 0 ? InvoiceStatus.REVIEW_REQUIRED : InvoiceStatus.APPROVED;
        if (initialStatus == InvoiceStatus.REVIEW_REQUIRED) pointsEarned = 0;
        var strategy = _context.Database.CreateExecutionStrategy();
        Invoice invoice = null!;
        int updatedPointsBalance = 0;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                invoice = new Invoice
                {
                    InvoiceId = Guid.NewGuid(),
                    CustomerId = customer.CustomerId,
                    TenantId = tenantId,
                    ExtractedMerchant = merchantName,
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = invoiceDate,
                    TotalAmount = totalAmount,
                    ConfidenceScore = (decimal)extractedData.Confidence,
                    FraudRiskScore = fraudRiskScore,
                    ProcessingStatus = initialStatus,
                    PointsAwarded = initialStatus == InvoiceStatus.APPROVED ? evalResult.TotalPointsAwarded : 0,
                    BasePointsEarned = initialStatus == InvoiceStatus.APPROVED ? evalResult.BasePointsEarned : 0,
                    ItemBonusPointsEarned = initialStatus == InvoiceStatus.APPROVED ? evalResult.ItemBonusPointsEarned : 0,
                    PointsBreakdownJson = evalResult.PointsBreakdownJson,
                    CashbackAwarded = initialStatus == InvoiceStatus.APPROVED ? evalResult.CashbackAwarded : 0m,
                    ReceiptImageUrl = imageUrl,
                    // أسباب المراجعة الداخلية لا تُعرض للعميل؛ تُحفظ مع رد الذكاء الاصطناعي للوحة التحكم
                    RawAiPayload = ReceiptReviewPayload.Serialize(extractedData, reviewReasons),
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Invoices.AddAsync(invoice);

                var wallet = await _context.CustomerWallets
                    .FirstOrDefaultAsync(w => w.CustomerId == customer.CustomerId && w.BalanceType == BalanceType.POINTS);

                if (wallet == null)
                {
                    wallet = new CustomerWallet
                    {
                        CustomerId = customer.CustomerId,
                        TenantId = tenantId,
                        BalanceType = BalanceType.POINTS,
                        CurrentBalance = invoice.PointsAwarded
                    };
                    await _context.CustomerWallets.AddAsync(wallet);
                }
                else if (initialStatus == InvoiceStatus.APPROVED)
                {
                    wallet.CurrentBalance += pointsEarned;
                }

                if (initialStatus == InvoiceStatus.APPROVED && pointsEarned > 0)
                {
                    var remarks = $"كسب نقاط من فاتورة: {merchantName} (#{invoiceNumber})";
                    if (evalResult.Remarks.Any())
                    {
                        remarks += $" [{string.Join("، ", evalResult.Remarks)}]";
                    }

                    var ledgerEntry = new WalletLedger
                    {
                        Wallet = wallet,
                        InvoiceId = invoice.InvoiceId,
                        AmountChanged = pointsEarned,
                        BalanceAfter = wallet.CurrentBalance,
                        TransactionType = WalletTransactionType.EARN_RECEIPT,
                        ReferenceRemarks = remarks,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.WalletLedger.AddAsync(ledgerEntry);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                updatedPointsBalance = (int)wallet.CurrentBalance;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "حدث خطأ أثناء معالجة الفاتورة داخل المعاملة (Transaction failed).");
                throw;
            }
        });

        _logger.LogInformation(
            "تمت معالجة الفاتورة بنجاح: {InvoiceId}, العميل: {CustomerId}, الحالة: {Status}, النقاط: +{Points}, الرصيد الحالي: {Balance}",
            invoice.InvoiceId, customer.CustomerId, invoice.ProcessingStatus, pointsEarned, updatedPointsBalance);

        var response = new InvoiceUploadResultDto
        {
            Success = true,
            Message = initialStatus == InvoiceStatus.APPROVED
                ? "تمت معالجة الفاتورة واحتساب نقاط الولاء بنجاح."
                : "تم استلام الفاتورة وتحويلها لطابور التدقيق والمراجعة.",
            PointsEarned = initialStatus == InvoiceStatus.APPROVED ? pointsEarned : 0,
            CurrentWalletPointsBalance = updatedPointsBalance,
            Confidence = extractedData.Confidence,
            Invoice = new InvoiceResponseDto
            {
                Id = invoice.InvoiceId,
                CustomerId = invoice.CustomerId,
                MerchantName = merchantName,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate.GetValueOrDefault(invoice.CreatedAt),
                TotalAmount = invoice.TotalAmount.GetValueOrDefault(0m),
                PointsEarned = invoice.PointsAwarded,
                Status = invoice.ProcessingStatus.ToString(),
                RejectionReason = invoice.RejectionReason,
                ImageUrl = invoice.ReceiptImageUrl,
                CreatedAt = invoice.CreatedAt
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// تقديم اعتراض رسمي من العميل على فاتورة مرفوضة لمراجعتها يدوياً من قبل الإدارة.
    /// </summary>
    [HttpPost("{id:guid}/dispute")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisputeInvoice(Guid id, [FromBody] CustomerDisputeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DisputeReason))
        {
            return BadRequest(new { success = false, message = "سبب ومبرر الاعتراض حقل إجباري." });
        }

        var loggedInUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;
        var userRole = User?.FindFirst(ClaimTypes.Role)?.Value;

        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id);
        if (invoice == null) return NotFound(new { success = false, message = "الفاتورة غير موجودة." });

        if (userRole != "SUPER_ADMIN" && !string.IsNullOrWhiteSpace(loggedInUserId) && invoice.CustomerId != loggedInUserId)
        {
            return Forbid();
        }

        invoice.ProcessingStatus = InvoiceStatus.DISPUTED;
        invoice.DisputeReason = req.DisputeReason.Trim();
        invoice.DisputedAt = DateTime.UtcNow;

        await _context.FraudReviewLogs.AddAsync(new FraudReviewLog
        {
            InvoiceId = invoice.InvoiceId,
            AssignedAuditorId = "SYSTEM",
            CalculatedRiskScore = invoice.FraudRiskScore,
            TriggeredFlags = "CUSTOMER_DISPUTE_FILED",
            AuditorDecision = "DISPUTED",
            DecisionReason = req.DisputeReason.Trim(),
            ResolvedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            PerformedBy = $"Customer ({invoice.CustomerId})",
            Action = "INVOICE_DISPUTE",
            EntityName = "Invoice",
            EntityId = invoice.InvoiceId.ToString(),
            Details = $"تقديم اعتراض على الفاتورة #{invoice.InvoiceNumber}: {req.DisputeReason}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم تقديم الاعتراض بنجاح وسيتم تدقيقه من قبل إدارة المول." });
    }

    /// <summary>
    /// إعادة رفع والتقاط صورة أوضح للفاتورة عند طلب إعادة التصوير من الإدارة.
    /// </summary>
    [HttpPost("{id:guid}/resubmit")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResubmitInvoice(Guid id, [FromForm] InvoiceUploadRequest req)
    {
        var loggedInUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;
        var userRole = User?.FindFirst(ClaimTypes.Role)?.Value;

        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id);
        if (invoice == null) return NotFound(new { success = false, message = "الفاتورة غير موجودة." });

        if (userRole != "SUPER_ADMIN" && !string.IsNullOrWhiteSpace(loggedInUserId) && invoice.CustomerId != loggedInUserId)
        {
            return Forbid();
        }

        if (req.File == null || req.File.Length == 0)
        {
            return BadRequest(new { success = false, message = "يرجى رفع ملف صالح لصورة الفاتورة الجديدة." });
        }

        string imageUrl;
        using (var uploadStream = req.File.OpenReadStream())
        {
            imageUrl = await _fileStorageService.SaveFileAsync(uploadStream, req.File.FileName, "invoices");
        }

        invoice.ReceiptImageUrl = imageUrl;
        invoice.ProcessingStatus = InvoiceStatus.PENDING;
        invoice.RejectionReason = null;
        invoice.ResubmissionNotes = null;

        await _context.FraudReviewLogs.AddAsync(new FraudReviewLog
        {
            InvoiceId = invoice.InvoiceId,
            AssignedAuditorId = "SYSTEM",
            CalculatedRiskScore = invoice.FraudRiskScore,
            TriggeredFlags = "RECEIPT_RESUBMITTED",
            AuditorDecision = "PENDING_REVIEW",
            DecisionReason = "تمت إعادة تصوير ورفع الفاتورة بواسطة العميل",
            ResolvedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            PerformedBy = $"Customer ({invoice.CustomerId})",
            Action = "INVOICE_RESUBMIT",
            EntityName = "Invoice",
            EntityId = invoice.InvoiceId.ToString(),
            Details = $"إعادة رفع صورة الفاتورة #{invoice.InvoiceNumber} بعد طلب إعادة التصوير",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تمت إعادة رفع الفاتورة بنجاح ودخلت طابور المراجعة والتدقيق." });
    }

    /// <summary>
    /// استعلام قائمة الفواتير مع إمكانية الفلترة حسب معرف العميل.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<InvoiceResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices([FromQuery] string? customerId)
    {
        var loggedInUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        var userRole = User?.FindFirst(ClaimTypes.Role)?.Value;

        var query = _context.Invoices.Include(i => i.Tenant).AsNoTracking();

        if (userRole != "SUPER_ADMIN" && !string.IsNullOrWhiteSpace(loggedInUserId))
        {
            query = query.Where(i => i.CustomerId == loggedInUserId);
        }
        else if (!string.IsNullOrWhiteSpace(customerId))
        {
            query = query.Where(i => i.CustomerId == customerId);
        }

        var list = await query
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

        return Ok(list);
    }

    /// <summary>
    /// استعلام تفاصيل فاتورة محددة بالمعرف.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(InvoiceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceById(Guid id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice == null)
        {
            return NotFound(new { success = false, message = $"الفاتورة بالمعرف '{id}' غير موجودة." });
        }

        var loggedInUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;
        var userRole = User?.FindFirst(ClaimTypes.Role)?.Value;

        if (userRole != "SUPER_ADMIN" && !string.IsNullOrWhiteSpace(loggedInUserId) && invoice.CustomerId != loggedInUserId)
        {
            return Forbid();
        }

        var result = new InvoiceResponseDto
        {
            Id = invoice.InvoiceId,
            CustomerId = invoice.CustomerId,
            MerchantName = invoice.Tenant != null ? invoice.Tenant.NameAr : (invoice.ExtractedMerchant ?? "متجر"),
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate.GetValueOrDefault(invoice.CreatedAt),
            TotalAmount = invoice.TotalAmount.GetValueOrDefault(0m),
            PointsEarned = invoice.PointsAwarded,
            Status = invoice.ProcessingStatus.ToString(),
            RejectionReason = invoice.RejectionReason,
            ImageUrl = invoice.ReceiptImageUrl,
            CreatedAt = invoice.CreatedAt
        };

        return Ok(result);
    }
}

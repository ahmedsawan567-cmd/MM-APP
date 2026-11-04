using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Api.DTOs;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

/// <summary>
/// قواعد القبول التلقائي للفواتير بعد قراءتها بالذكاء الاصطناعي.
/// </summary>
public class InvoiceReviewRulesTests
{
    private const string CustomerId = "CUST-RULES";
    private const string StoreId = "TEN-KADI";

    private sealed class StubOcr(ExtractedReceiptDto result) : IReceiptOcrService
    {
        public ReceiptExtractionContext? LastContext { get; private set; }
        public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream) => Task.FromResult(result);
        public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream, ReceiptExtractionContext context)
        {
            LastContext = context;
            return Task.FromResult(result);
        }
    }

    private sealed class NoopStorage : IFileStorageService
    {
        public Task<string> SaveFileAsync(Stream s, string fileName, string folder) => Task.FromResult($"/uploads/{folder}/{fileName}");
        public Task<bool> DeleteFileAsync(string path) => Task.FromResult(true);
    }

    private static async Task<AppDbContext> CreateDbAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        db.Tenants.Add(new Tenant
        {
            TenantId = StoreId,
            NameAr = "كافي كادي",
            NameEn = "Kadi Specialty Coffee",
            Category = "Cafes",
            UnitNumber = "G-10",
            IsActive = true,
        });
        db.Customers.Add(new Customer
        {
            CustomerId = CustomerId,
            FullName = "عميل الاختبار",
            PhoneNumber = "0910000001",
            RegisteredAt = DateTime.UtcNow,
        });
        db.CustomerWallets.Add(new CustomerWallet { CustomerId = CustomerId, BalanceType = BalanceType.POINTS, CurrentBalance = 0 });
        await db.SaveChangesAsync();
        return db;
    }

    private static ExtractedReceiptDto GoodReceipt() => new()
    {
        Provider = "Claude",
        MerchantName = "KADI COFFEE",
        MatchedTenantId = StoreId,
        InvoiceNumber = "KD-1",
        InvoiceDate = DateTime.UtcNow.Date,
        TotalAmount = 100m,
        Currency = "LYD",
        Confidence = 0.97,
    };

    private static async Task<(IActionResult Result, Invoice? Invoice, StubOcr Ocr)> UploadAsync(ExtractedReceiptDto extracted)
    {
        var db = await CreateDbAsync();
        var ocr = new StubOcr(extracted);
        var controller = new InvoicesController(db, ocr, new NoopStorage(),
            new FlexibleLoyaltyEvaluationService(db), NullLogger<InvoicesController>.Instance);

        var bytes = Encoding.UTF8.GetBytes("image");
        var result = await controller.UploadInvoice(new InvoiceUploadRequest
        {
            CustomerId = CustomerId,
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "r.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg",
            },
        });

        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync();
        return (result, invoice, ocr);
    }

    private static List<string> Reasons(Invoice invoice) =>
        ReceiptReviewPayload.TryParse(invoice.RawAiPayload)?.ReviewReasons ?? new();

    [Fact]
    public async Task CleanReceipt_FromMallStore_IsAutoApproved_UsingAiStoreMatch()
    {
        var (result, invoice, ocr) = await UploadAsync(GoodReceipt());

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(InvoiceStatus.APPROVED, invoice!.ProcessingStatus);
        Assert.Equal(StoreId, invoice.TenantId); // مطابقة "KADI COFFEE" جاءت من الذكاء الاصطناعي
        Assert.True(invoice.PointsAwarded > 0);
        Assert.Empty(Reasons(invoice));
        // قائمة المتاجر أُرسلت لمحرك القراءة
        Assert.Contains(ocr.LastContext!.KnownStores, s => s.TenantId == StoreId);
    }

    [Fact]
    public async Task NotAReceipt_IsRejectedWithoutSaving()
    {
        var r = GoodReceipt();
        r.IsReceipt = false;
        var (result, invoice, _) = await UploadAsync(r);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(invoice);
    }

    [Fact]
    public async Task AiUnavailable_SavesInvoiceForManualReview_WithZeroPoints()
    {
        var (result, invoice, _) = await UploadAsync(new ExtractedReceiptDto
        {
            Provider = "Claude",
            RequiresManualReview = true,
            AuthenticityConcerns = ["خدمة القراءة الآلية غير متاحة مؤقتاً."],
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(InvoiceStatus.REVIEW_REQUIRED, invoice!.ProcessingStatus);
        Assert.Equal(0, invoice.PointsAwarded);
        Assert.Contains(Reasons(invoice), x => x.Contains("تعذّرت القراءة الآلية"));
    }

    public static TheoryData<string, Action<ExtractedReceiptDto>, string> ReviewCases => new()
    {
        { "low confidence", r => r.Confidence = 0.5, "ثقة القراءة منخفضة" },
        { "tampering", r => r.AuthenticityConcerns.Add("الرقم الإجمالي معدّل"), "الرقم الإجمالي معدّل" },
        { "unknown store", r => { r.MatchedTenantId = null; r.MerchantName = "Some Other Shop"; }, "المتجر غير معروف" },
        { "old invoice", r => r.InvoiceDate = DateTime.UtcNow.AddDays(-30), "أقدم من" },
        { "future date", r => r.InvoiceDate = DateTime.UtcNow.AddDays(5), "في المستقبل" },
        { "large amount", r => r.TotalAmount = 9000m, "مبلغ مرتفع" },
        { "foreign currency", r => r.Currency = "USD", "عملة" },
    };

    [Theory]
    [MemberData(nameof(ReviewCases))]
    public async Task SuspiciousReceipt_GoesToReview_WithoutPoints(string _, Action<ExtractedReceiptDto> mutate, string expectedReason)
    {
        var r = GoodReceipt();
        mutate(r);
        var (result, invoice, _) = await UploadAsync(r);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(InvoiceStatus.REVIEW_REQUIRED, invoice!.ProcessingStatus);
        Assert.Equal(0, invoice.PointsAwarded);
        Assert.Contains(Reasons(invoice), x => x.Contains(expectedReason));
        // أسباب المراجعة لا تظهر للعميل
        Assert.Null(invoice.RejectionReason);
    }
}

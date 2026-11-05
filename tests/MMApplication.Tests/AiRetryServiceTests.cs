using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Services;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class AiRetryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mm-retry-" + Guid.NewGuid().ToString("N"));

    private sealed class QueueOcr(params ExtractedReceiptDto[] results) : IReceiptOcrService
    {
        private readonly Queue<ExtractedReceiptDto> _q = new(results);
        public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream s) => Task.FromResult(_q.Dequeue());
    }

    private sealed class Env(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Testing";
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private ServiceProvider Build(IReceiptOcrService ocr, string dbName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "uploads"));
        File.WriteAllBytes(Path.Combine(_root, "uploads", "r.jpg"), [0xFF, 0xD8, 0xFF]);
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddSingleton(ocr);
        services.AddSingleton<IWebHostEnvironment>(new Env(_root));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddScoped<IFlexibleLoyaltyEvaluationService, FlexibleLoyaltyEvaluationService>();
        services.AddScoped<InvoiceReviewService>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant { TenantId = "KADI", NameAr = "كافي كادي", NameEn = "Kadi", Category = "مطاعم", IsActive = true });
        db.Customers.Add(new Customer { CustomerId = "C1", FullName = "سارة", PhoneNumber = "0910000001" });
        db.CustomerWallets.Add(new CustomerWallet { CustomerId = "C1", BalanceType = BalanceType.POINTS });
        var inv = new Invoice
        {
            CustomerId = "C1",
            ExtractedMerchant = "بانتظار القراءة اليدوية",
            InvoiceNumber = "",
            TotalAmount = 0,
            ProcessingStatus = InvoiceStatus.REVIEW_REQUIRED,
            ReceiptImageUrl = "/uploads/r.jpg",
            NextAiRetryAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.Invoices.Add(inv);
        await db.SaveChangesAsync();
        return inv.InvoiceId;
    }

    private static ExtractedReceiptDto QuotaFailure() => new() { Provider = "Gemini", RequiresManualReview = true, Retryable = true, AuthenticityConcerns = ["انتهت الحصة"] };

    private static ExtractedReceiptDto Good() => new()
    {
        Provider = "Gemini", MerchantName = "كافي كادي", MatchedTenantId = "KADI", InvoiceNumber = "KC-777",
        InvoiceDate = DateTime.UtcNow, TotalAmount = 80m, Confidence = 0.97, Currency = "LYD",
    };

    [Fact]
    public void Schedule_BacksOffAndStops()
    {
        var now = DateTime.UtcNow;
        Assert.Equal(now.AddMinutes(5), AiRetryService.NextAttempt(0, now));
        Assert.True(AiRetryService.NextAttempt(3, now) > AiRetryService.NextAttempt(2, now));
        Assert.Null(AiRetryService.NextAttempt(AiRetryService.MaxAttempts, now));
    }

    [Fact]
    public async Task StillOverQuota_ReschedulesLater()
    {
        using var sp = Build(new QueueOcr(QuotaFailure()), Guid.NewGuid().ToString());
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await SeedAsync(db);

        Assert.Equal(1, await AiRetryService.ProcessDueAsync(scope.ServiceProvider));
        var inv = await db.Invoices.FindAsync(id);
        Assert.Equal(1, inv!.AiRetryCount);
        Assert.True(inv.NextAiRetryAt > DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(InvoiceStatus.REVIEW_REQUIRED, inv.ProcessingStatus);
        // غير مستحقة الآن
        Assert.Equal(0, await AiRetryService.ProcessDueAsync(scope.ServiceProvider));
    }

    [Fact]
    public async Task SuccessfulReread_AutoApprovesAndAwardsPoints()
    {
        using var sp = Build(new QueueOcr(Good()), Guid.NewGuid().ToString());
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await SeedAsync(db);

        Assert.Equal(1, await AiRetryService.ProcessDueAsync(scope.ServiceProvider));
        var inv = await db.Invoices.AsNoTracking().FirstAsync(i => i.InvoiceId == id);
        Assert.Equal(InvoiceStatus.APPROVED, inv.ProcessingStatus);
        Assert.Equal("KC-777", inv.InvoiceNumber);
        Assert.Equal("KADI", inv.TenantId);
        Assert.Null(inv.NextAiRetryAt);
        Assert.Contains(db.Notifications, n => n.RecipientId == "C1" && n.Type == "INVOICE_APPROVED");
    }

    [Fact]
    public async Task RereadOfDuplicateNumber_StaysInReview()
    {
        var dbName = Guid.NewGuid().ToString();
        using var sp = Build(new QueueOcr(Good()), dbName);
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await SeedAsync(db);
        db.Invoices.Add(new Invoice { CustomerId = "C2", TenantId = "KADI", InvoiceNumber = "KC-777", TotalAmount = 80m, ProcessingStatus = InvoiceStatus.APPROVED });
        await db.SaveChangesAsync();

        await AiRetryService.ProcessDueAsync(scope.ServiceProvider);
        var inv = await db.Invoices.AsNoTracking().FirstAsync(i => i.InvoiceId == id);
        Assert.Equal(InvoiceStatus.REVIEW_REQUIRED, inv.ProcessingStatus);
        Assert.Contains("مستخدم", inv.RawAiPayload);
    }
}

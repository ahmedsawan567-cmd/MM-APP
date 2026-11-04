using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Api.Services;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class InvoiceReviewServiceTests
{
    private const string CustomerId = "CUST-REV";

    private static async Task<(AppDbContext Db, InvoiceReviewService Service)> SetupAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        db.Tenants.Add(new Tenant { TenantId = "TEN-A", NameAr = "متجر أ", NameEn = "Store A", Category = "Fashion", UnitNumber = "G-1" });
        db.Customers.Add(new Customer { CustomerId = CustomerId, FullName = "عميل", PhoneNumber = "0910000099", RegisteredAt = DateTime.UtcNow });
        db.CustomerWallets.Add(new CustomerWallet { CustomerId = CustomerId, BalanceType = BalanceType.POINTS, CurrentBalance = 10 });
        await db.SaveChangesAsync();

        var service = new InvoiceReviewService(db, new FlexibleLoyaltyEvaluationService(db), NullLogger<InvoiceReviewService>.Instance);
        return (db, service);
    }

    private static Invoice AddInvoice(AppDbContext db, string number = "A-1", decimal? amount = 150m,
        InvoiceStatus status = InvoiceStatus.REVIEW_REQUIRED, string? tenantId = "TEN-A")
    {
        var invoice = new Invoice
        {
            CustomerId = CustomerId,
            TenantId = tenantId,
            ExtractedMerchant = "Store A",
            InvoiceNumber = number,
            InvoiceDate = DateTime.UtcNow,
            TotalAmount = amount,
            ProcessingStatus = status,
            ReceiptImageUrl = "/uploads/invoices/x.jpg",
        };
        db.Invoices.Add(invoice);
        db.SaveChanges();
        return invoice;
    }

    private static async Task<decimal> BalanceAsync(AppDbContext db) =>
        (await db.CustomerWallets.AsNoTracking().FirstAsync(w => w.CustomerId == CustomerId)).CurrentBalance;

    [Fact]
    public async Task Approve_AwardsPointsWithLoyaltyEngine_AndWritesLedgerAndAudit()
    {
        var (db, service) = await SetupAsync();
        var invoice = AddInvoice(db);

        var outcome = await service.ApproveAsync(invoice.InvoiceId, "auditor-1");

        Assert.True(outcome.Success);
        Assert.True(outcome.PointsChanged > 0);
        var saved = await db.Invoices.AsNoTracking().FirstAsync();
        Assert.Equal(InvoiceStatus.APPROVED, saved.ProcessingStatus);
        Assert.Equal(outcome.PointsChanged, saved.PointsAwarded);
        Assert.Equal(10 + outcome.PointsChanged, await BalanceAsync(db));
        Assert.Single(await db.WalletLedger.ToListAsync());
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "INVOICE_APPROVE" && a.PerformedBy == "auditor-1");
    }

    [Fact]
    public async Task Approve_AppliesReviewerCorrections_BeforeCalculatingPoints()
    {
        var (db, service) = await SetupAsync();
        var invoice = AddInvoice(db, amount: 5000m, tenantId: null);

        var outcome = await service.ApproveAsync(invoice.InvoiceId, "auditor-1", new InvoiceCorrection
        {
            TenantId = "TEN-A",
            TotalAmount = 50m,
            InvoiceNumber = "A-CORRECTED",
        });

        Assert.True(outcome.Success);
        var saved = await db.Invoices.AsNoTracking().FirstAsync();
        Assert.Equal("TEN-A", saved.TenantId);
        Assert.Equal(50m, saved.TotalAmount);
        Assert.Equal("A-CORRECTED", saved.InvoiceNumber);
        // النقاط محسوبة على 50 وليس 5000
        Assert.True(outcome.PointsChanged <= 10);
        Assert.Contains("تصحيحات", (await db.AuditLogs.FirstAsync()).Details);
    }

    [Fact]
    public async Task Approve_WithoutAmount_IsRefused()
    {
        var (db, service) = await SetupAsync();
        var invoice = AddInvoice(db, amount: null);

        var outcome = await service.ApproveAsync(invoice.InvoiceId, "auditor-1");

        Assert.False(outcome.Success);
        Assert.Equal(10, await BalanceAsync(db));
    }

    [Fact]
    public async Task Approve_DuplicateOfApprovedInvoice_IsRefused()
    {
        var (db, service) = await SetupAsync();
        AddInvoice(db, number: "DUP-1", status: InvoiceStatus.APPROVED);
        var second = AddInvoice(db, number: "DUP-1");

        var outcome = await service.ApproveAsync(second.InvoiceId, "auditor-1");

        Assert.False(outcome.Success);
        Assert.Equal(409, outcome.StatusCode);
    }

    [Fact]
    public async Task RejectingApprovedInvoice_ReversesItsPoints()
    {
        var (db, service) = await SetupAsync();
        var invoice = AddInvoice(db);
        var approved = await service.ApproveAsync(invoice.InvoiceId, "auditor-1");
        Assert.True(approved.PointsChanged > 0);

        var rejected = await service.RejectAsync(invoice.InvoiceId, "auditor-2", "الفاتورة مزورة");

        Assert.True(rejected.Success);
        Assert.Equal(-approved.PointsChanged, rejected.PointsChanged);
        Assert.Equal(10, await BalanceAsync(db));
        var saved = await db.Invoices.AsNoTracking().FirstAsync();
        Assert.Equal(InvoiceStatus.REJECTED, saved.ProcessingStatus);
        Assert.Equal(0, saved.PointsAwarded);
        Assert.Equal("الفاتورة مزورة", saved.RejectionReason);
        Assert.Equal(2, await db.WalletLedger.CountAsync());
    }

    [Fact]
    public async Task RequestResubmission_SetsStatusAndNote()
    {
        var (db, service) = await SetupAsync();
        var invoice = AddInvoice(db);

        var outcome = await service.RequestResubmissionAsync(invoice.InvoiceId, "auditor-1", "الصورة مقطوعة");

        Assert.True(outcome.Success);
        var saved = await db.Invoices.AsNoTracking().FirstAsync();
        Assert.Equal(InvoiceStatus.RESUBMISSION_REQUESTED, saved.ProcessingStatus);
        Assert.Equal("الصورة مقطوعة", saved.RejectionReason);
    }

    [Fact]
    public async Task StoreManager_CannotApproveFromDashboard()
    {
        var (db, service) = await SetupAsync();
        var invoice = AddInvoice(db);
        var controller = new AdminDashboardController(db, NullLogger<AdminDashboardController>.Instance, review: service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "USR-MGR"),
                        new Claim(ClaimTypes.Role, "storemanager"),
                        new Claim("SystemRole", "STORE_MANAGER"),
                        new Claim("TenantId", "TEN-A"),
                    }, "Test")),
                },
            },
        };

        var result = await controller.ReviewApprove(invoice.InvoiceId, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(InvoiceStatus.REVIEW_REQUIRED, (await db.Invoices.AsNoTracking().FirstAsync()).ProcessingStatus);
    }
}

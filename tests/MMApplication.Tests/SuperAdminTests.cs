using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Application.DTOs;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class SuperAdminTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Overview_ShouldReturnCalculatedKpis()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new SuperAdminController(context, hasher, new FlexibleLoyaltyEvaluationService(context), NullLogger<SuperAdminController>.Instance);

        var result = await controller.GetOverview();
        var ok = Assert.IsType<OkObjectResult>(result);
        var overview = Assert.IsType<SuperAdminOverviewDto>(ok.Value);

        Assert.True(overview.TotalSalesVolume > 0);
        Assert.NotEmpty(overview.ScanActivityPace);
        Assert.NotEmpty(overview.CategoryDistribution);
        Assert.NotEmpty(overview.TopPerformingStores);
    }

    [Fact]
    public async Task Stores_ShouldCreateAndRetrieveTenants()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        var controller = new SuperAdminController(context, hasher, new FlexibleLoyaltyEvaluationService(context), NullLogger<SuperAdminController>.Instance);

        var createResult = await controller.CreateStore(new CreateStoreTenantRequest
        {
            Name = "Adidas Store",
            Category = "ملابس رياضية",
            FloorNumber = "1",
            UnitNumber = "105",
            LoyaltyType = StoreLoyaltyType.MallPoints,
            CommissionRate = 5.5m,
            ContactPerson = "علي",
            ContactPhone = "0910001122"
        });

        Assert.IsType<CreatedAtActionResult>(createResult);

        var listResult = await controller.GetStores(null, null);
        var ok = Assert.IsType<OkObjectResult>(listResult);
        var stores = Assert.IsAssignableFrom<IEnumerable<StoreTenantDto>>(ok.Value);

        Assert.Contains(stores, s => s.Name == "Adidas Store");
    }

    [Fact]
    public async Task FraudHub_ReviewInvoice_ShouldApproveOrRejectAndRecordAuditLog()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new SuperAdminController(context, hasher, new FlexibleLoyaltyEvaluationService(context), NullLogger<SuperAdminController>.Instance);

        var suspectInvoice = await context.Invoices.FirstOrDefaultAsync(i => i.FraudRiskScore >= 31 && (i.ProcessingStatus == InvoiceStatus.PENDING || i.ProcessingStatus == InvoiceStatus.REVIEW_REQUIRED));
        Assert.NotNull(suspectInvoice);

        var reviewResult = await controller.ReviewFraudInvoice(suspectInvoice.InvoiceId, new ReviewInvoiceRequest
        {
            Action = "APPROVE"
        });

        Assert.IsType<OkObjectResult>(reviewResult);

        var updated = await context.Invoices.FindAsync(suspectInvoice.InvoiceId);
        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.APPROVED, updated.ProcessingStatus);
        Assert.Equal(0, updated.FraudRiskScore);

        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == suspectInvoice.InvoiceId.ToString());
        Assert.NotNull(auditLog);
        Assert.Equal("FRAUD_QUEUE_APPROVE", auditLog.Action);
    }

    [Fact]
    public async Task Settlements_ShouldSettleBatch()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new SuperAdminController(context, hasher, new FlexibleLoyaltyEvaluationService(context), NullLogger<SuperAdminController>.Instance);

        var pendingSettlement = await context.TenantSettlements.FirstOrDefaultAsync(s => s.SettlementStatus == SettlementStatus.Pending);
        Assert.NotNull(pendingSettlement);

        var result = await controller.SettleStoreBatch(pendingSettlement.SettlementId);
        Assert.IsType<OkObjectResult>(result);

        var updated = await context.TenantSettlements.FindAsync(pendingSettlement.SettlementId);
        Assert.NotNull(updated);
        Assert.Equal(SettlementStatus.Settled, updated.SettlementStatus);
        Assert.NotNull(updated.SettledAt);
    }

    [Fact]
    public async Task Customers_ShouldAdjustPointsAndToggleSuspend()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new SuperAdminController(context, hasher, new FlexibleLoyaltyEvaluationService(context), NullLogger<SuperAdminController>.Instance);

        var customer = await context.Customers.FirstOrDefaultAsync(c => c.CustomerId == "CUST-001");
        Assert.NotNull(customer);

        // Adjust points
        var adjustResult = await controller.AdjustCustomerPoints(customer.CustomerId, new AdjustCustomerPointsRequest
        {
            PointsDelta = 50,
            Reason = "نقاط ولاء تشجيعية"
        });
        Assert.IsType<OkObjectResult>(adjustResult);

        var wallet = await context.CustomerWallets.FirstOrDefaultAsync(w => w.CustomerId == customer.CustomerId && w.BalanceType == BalanceType.POINTS);
        Assert.NotNull(wallet);
        Assert.Equal(200, wallet.CurrentBalance); // 150 + 50 = 200

        // Toggle Suspend
        var toggleResult = await controller.ToggleCustomerSuspension(customer.CustomerId);
        Assert.IsType<OkObjectResult>(toggleResult);

        var updatedCustomer = await context.Customers.FindAsync(customer.CustomerId);
        Assert.NotNull(updatedCustomer);
        Assert.True(updatedCustomer.IsFrozen);
    }
}

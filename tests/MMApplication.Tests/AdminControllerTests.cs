using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Application.DTOs;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Tests;

public class AdminControllerTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private void SetAdminUser(ControllerBase controller)
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new(ClaimTypes.NameIdentifier, "ADMIN-001"),
            new(ClaimTypes.Name, "مدير النظام"),
            new(ClaimTypes.Role, "Admin")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };
    }

    [Fact]
    public async Task GetDashboardMetrics_ShouldReturnAccurateAggregations()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer1 = new Customer { CustomerId = "C1", FullName = "عميل 1", PhoneNumber = "0911" };
        var customer2 = new Customer { CustomerId = "C2", FullName = "عميل 2", PhoneNumber = "0912" };

        var invoice1 = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            CustomerId = "C1",
            ExtractedMerchant = "Zara",
            InvoiceNumber = "Z-1",
            TotalAmount = 200m,
            PointsAwarded = 20,
            ProcessingStatus = InvoiceStatus.APPROVED
        };
        var invoice2 = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            CustomerId = "C2",
            ExtractedMerchant = "H&M",
            InvoiceNumber = "H-1",
            TotalAmount = 100m,
            PointsAwarded = 10,
            ProcessingStatus = InvoiceStatus.PENDING
        };

        var wallet1 = new CustomerWallet { CustomerId = "C1", BalanceType = BalanceType.POINTS, CurrentBalance = 30 };
        var tx1 = new WalletLedger { Wallet = wallet1, AmountChanged = 50, BalanceAfter = 50, TransactionType = WalletTransactionType.EARN_RECEIPT, ReferenceRemarks = "كسب" };
        var tx2 = new WalletLedger { Wallet = wallet1, AmountChanged = -20, BalanceAfter = 30, TransactionType = WalletTransactionType.REDEEM_PURCHASE, ReferenceRemarks = "استبدال" };

        await context.Customers.AddRangeAsync(customer1, customer2);
        await context.CustomerWallets.AddAsync(wallet1);
        await context.Invoices.AddRangeAsync(invoice1, invoice2);
        await context.WalletLedger.AddRangeAsync(tx1, tx2);
        await context.SaveChangesAsync();

        var controller = new AdminController(context, NullLogger<AdminController>.Instance);
        SetAdminUser(controller);

        // Act
        var result = await controller.GetDashboardMetrics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var metrics = Assert.IsType<DashboardMetricsDto>(okResult.Value);

        Assert.Equal(2, metrics.TotalCustomers);
        Assert.Equal(2, metrics.TotalInvoices);
        Assert.Equal(1, metrics.ApprovedInvoicesCount);
        Assert.Equal(1, metrics.PendingInvoicesCount);
        Assert.Equal(200m, metrics.TotalSalesVolume);
        Assert.Equal(50, metrics.TotalPointsIssued);
        Assert.Equal(20, metrics.TotalPointsRedeemed);
    }

    [Fact]
    public async Task GetTopMerchants_ShouldRankMerchantsBySales()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer { CustomerId = "C1", FullName = "عميل", PhoneNumber = "0911" };

        var inv1 = new Invoice { CustomerId = "C1", ExtractedMerchant = "Zara", InvoiceNumber = "Z-1", TotalAmount = 500m, PointsAwarded = 50, ProcessingStatus = InvoiceStatus.APPROVED };
        var inv2 = new Invoice { CustomerId = "C1", ExtractedMerchant = "Zara", InvoiceNumber = "Z-2", TotalAmount = 300m, PointsAwarded = 30, ProcessingStatus = InvoiceStatus.APPROVED };
        var inv3 = new Invoice { CustomerId = "C1", ExtractedMerchant = "Starbucks", InvoiceNumber = "S-1", TotalAmount = 50m, PointsAwarded = 5, ProcessingStatus = InvoiceStatus.APPROVED };

        await context.Customers.AddAsync(customer);
        await context.Invoices.AddRangeAsync(inv1, inv2, inv3);
        await context.SaveChangesAsync();

        var controller = new AdminController(context, NullLogger<AdminController>.Instance);
        SetAdminUser(controller);

        // Act
        var result = await controller.GetTopMerchants();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var top = Assert.IsAssignableFrom<IEnumerable<TopMerchantDto>>(okResult.Value).ToList();

        Assert.Equal(2, top.Count);
        Assert.Equal("Zara", top[0].MerchantName);
        Assert.Equal(800m, top[0].TotalSales);
        Assert.Equal(2, top[0].InvoicesCount);
        Assert.Equal("Starbucks", top[1].MerchantName);
    }

    [Fact]
    public async Task ApproveInvoice_ShouldAwardPointsAndUpdateStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var wallet = new CustomerWallet { CustomerId = "CUST-APPROVE", BalanceType = BalanceType.POINTS, CurrentBalance = 10 };
        var customer = new Customer
        {
            CustomerId = "CUST-APPROVE",
            FullName = "صالح أحمد",
            PhoneNumber = "091555",
            Wallets = new List<CustomerWallet> { wallet }
        };
        var pendingInvoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            CustomerId = "CUST-APPROVE",
            ExtractedMerchant = "Apple Store",
            InvoiceNumber = "AP-999",
            TotalAmount = 350.00m,
            ProcessingStatus = InvoiceStatus.PENDING
        };

        await context.Customers.AddAsync(customer);
        await context.Invoices.AddAsync(pendingInvoice);
        await context.SaveChangesAsync();

        var controller = new AdminController(context, NullLogger<AdminController>.Instance);
        SetAdminUser(controller);

        // Act
        var result = await controller.ApproveInvoice(pendingInvoice.InvoiceId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var invoiceDto = Assert.IsType<InvoiceResponseDto>(okResult.Value);

        Assert.Equal("APPROVED", invoiceDto.Status);
        Assert.Equal(35, invoiceDto.PointsEarned); // 350 / 10 = 35

        var updatedWallet = await context.CustomerWallets.FirstAsync(w => w.CustomerId == "CUST-APPROVE" && w.BalanceType == BalanceType.POINTS);
        Assert.Equal(45, updatedWallet.CurrentBalance); // 10 + 35 = 45

        var pointTx = await context.WalletLedger.FirstOrDefaultAsync(t => t.InvoiceId == pendingInvoice.InvoiceId);
        Assert.NotNull(pointTx);
        Assert.Equal(35, pointTx.AmountChanged);
    }

    [Fact]
    public async Task RejectInvoice_ShouldUpdateStatusAndReason()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer { CustomerId = "CUST-REJECT", FullName = "عميل", PhoneNumber = "091666" };
        var pendingInvoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            CustomerId = "CUST-REJECT",
            ExtractedMerchant = "Unclear Store",
            InvoiceNumber = "UNC-1",
            TotalAmount = 50.00m,
            ProcessingStatus = InvoiceStatus.PENDING
        };

        await context.Customers.AddAsync(customer);
        await context.Invoices.AddAsync(pendingInvoice);
        await context.SaveChangesAsync();

        var controller = new AdminController(context, NullLogger<AdminController>.Instance);
        SetAdminUser(controller);

        // Act
        var result = await controller.RejectInvoice(pendingInvoice.InvoiceId, new RejectInvoiceRequestDto { Reason = "الصورة غير واضحة وتاريخ الفاتورة قديم" });

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var invoiceDto = Assert.IsType<InvoiceResponseDto>(okResult.Value);

        Assert.Equal("REJECTED", invoiceDto.Status);
        Assert.Equal("الصورة غير واضحة وتاريخ الفاتورة قديم", invoiceDto.RejectionReason);
    }

    [Fact]
    public async Task AdjustCustomerPoints_ShouldUpdateWalletAndLogTransaction()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var wallet = new CustomerWallet { CustomerId = "CUST-ADJUST", BalanceType = BalanceType.POINTS, CurrentBalance = 50 };
        var customer = new Customer
        {
            CustomerId = "CUST-ADJUST",
            FullName = "مروان كمال",
            PhoneNumber = "091777",
            Wallets = new List<CustomerWallet> { wallet }
        };
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var controller = new AdminController(context, NullLogger<AdminController>.Instance);
        SetAdminUser(controller);

        // Act: Add 30 bonus points
        var result = await controller.AdjustCustomerPoints("CUST-ADJUST", new AdjustPointsDto
        {
            PointsAmount = 30,
            Reason = "مكافأة بمناسبة عيد الفطر"
        });

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var updatedWallet = await context.CustomerWallets.FirstAsync(w => w.CustomerId == "CUST-ADJUST" && w.BalanceType == BalanceType.POINTS);
        Assert.Equal(80, updatedWallet.CurrentBalance); // 50 + 30 = 80

        var tx = await context.WalletLedger.FirstOrDefaultAsync(t => t.TransactionType == WalletTransactionType.MANUAL_ADJUSTMENT);
        Assert.NotNull(tx);
        Assert.Equal(30, tx.AmountChanged);
        Assert.Equal(80, tx.BalanceAfter);
    }
}

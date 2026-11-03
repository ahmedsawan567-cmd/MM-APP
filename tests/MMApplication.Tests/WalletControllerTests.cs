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

public class WalletControllerTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private void SetControllerUser(ControllerBase controller, string userId, string role = "Customer")
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, "اسم المستخدم"),
            new(ClaimTypes.Role, role)
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };
    }

    [Fact]
    public async Task GetWalletSummary_ShouldCalculateSummaryCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = "CUST-SUMMARY";
        var wallet = new CustomerWallet { CustomerId = customerId, BalanceType = BalanceType.POINTS, CurrentBalance = 80 };
        var customer = new Customer
        {
            CustomerId = customerId,
            FullName = "نور الهدى",
            PhoneNumber = "0917778899",
            Wallets = new List<CustomerWallet> { wallet }
        };

        var tx1 = new WalletLedger
        {
            Wallet = wallet,
            AmountChanged = 100,
            BalanceAfter = 100,
            TransactionType = WalletTransactionType.EARN_RECEIPT,
            ReferenceRemarks = "فاتورة",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var tx2 = new WalletLedger
        {
            Wallet = wallet,
            AmountChanged = -20,
            BalanceAfter = 80,
            TransactionType = WalletTransactionType.REDEEM_PURCHASE,
            ReferenceRemarks = "استبدال",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        await context.Customers.AddAsync(customer);
        await context.CustomerWallets.AddAsync(wallet);
        await context.WalletLedger.AddRangeAsync(tx1, tx2);
        await context.SaveChangesAsync();

        var controller = new WalletController(context, NullLogger<WalletController>.Instance);
        SetControllerUser(controller, customerId);

        // Act
        var result = await controller.GetWalletSummary();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<WalletSummaryDto>(okResult.Value);

        Assert.Equal(80, summary.PointsBalance);
        Assert.Equal(100, summary.TotalPointsEarned);
        Assert.Equal(20, summary.TotalPointsSpent);
    }

    [Fact]
    public async Task ValidateAndUseVoucher_WhenValid_ShouldMarkAsUsed()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer
        {
            CustomerId = "CUST-VOUCHER",
            FullName = "كريم سامي",
            PhoneNumber = "0913332211"
        };
        var reward = new Reward
        {
            Title = "خصم 10 ستاربكس",
            PointsCost = 50,
            DiscountValue = 10.00m
        };
        var redemption = new RewardRedemption
        {
            CustomerId = "CUST-VOUCHER",
            Reward = reward,
            RedemptionCode = "MM-TEST1234",
            PointsSpent = 50,
            Status = RedemptionStatus.ACTIVE,
            RedeemedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(10)
        };

        await context.Customers.AddAsync(customer);
        await context.Rewards.AddAsync(reward);
        await context.Redemptions.AddAsync(redemption);
        await context.SaveChangesAsync();

        var controller = new WalletController(context, NullLogger<WalletController>.Instance);
        SetControllerUser(controller, "MERCHANT-ID", role: "Merchant");

        // Act
        var result = await controller.ValidateAndUseVoucher("MM-TEST1234", new UseVoucherRequestDto { MerchantName = "Starbucks" });

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var updatedRedemption = await context.Redemptions.FirstAsync(r => r.RedemptionCode == "MM-TEST1234");
        Assert.Equal(RedemptionStatus.USED, updatedRedemption.Status);
        Assert.NotNull(updatedRedemption.UsedAt);
        Assert.Equal("Starbucks", updatedRedemption.UsedByMerchant);
    }

    [Fact]
    public async Task ValidateAndUseVoucher_WhenAlreadyUsed_ShouldReturnBadRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer { CustomerId = "CUST-VOUCHER-2", FullName = "جمال", PhoneNumber = "0914443322" };
        var reward = new Reward { Title = "كوبون", PointsCost = 50 };
        var redemption = new RewardRedemption
        {
            CustomerId = "CUST-VOUCHER-2",
            Reward = reward,
            RedemptionCode = "MM-ALREADYUSED",
            PointsSpent = 50,
            Status = RedemptionStatus.USED,
            UsedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(10)
        };

        await context.Customers.AddAsync(customer);
        await context.Rewards.AddAsync(reward);
        await context.Redemptions.AddAsync(redemption);
        await context.SaveChangesAsync();

        var controller = new WalletController(context, NullLogger<WalletController>.Instance);
        SetControllerUser(controller, "MERCHANT-ID", role: "Merchant");

        // Act
        var result = await controller.ValidateAndUseVoucher("MM-ALREADYUSED", null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}

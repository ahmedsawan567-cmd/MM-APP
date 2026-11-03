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

public class RewardsControllerTests
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
            new(ClaimTypes.Role, role)
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };
    }

    [Fact]
    public async Task GetRewards_ShouldReturnActiveRewards()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reward1 = new Reward { Title = "كوبون ستاربكس", PointsCost = 50, IsActive = true };
        var reward2 = new Reward { Title = "مكافأة غير نشطة", PointsCost = 100, IsActive = false };
        await context.Rewards.AddRangeAsync(reward1, reward2);
        await context.SaveChangesAsync();

        var controller = new RewardsController(context, NullLogger<RewardsController>.Instance);

        // Act
        var result = await controller.GetRewards(onlyActive: true);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var rewards = Assert.IsAssignableFrom<IEnumerable<RewardDto>>(okResult.Value);
        Assert.Single(rewards);
        Assert.Equal("كوبون ستاربكس", rewards.First().Title);
    }

    [Fact]
    public async Task RedeemReward_WithSufficientPoints_ShouldDeductPointsAndGenerateVoucher()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = "CUST-REDEEM-OK";
        var customer = new Customer
        {
            CustomerId = customerId,
            FullName = "سمير حسن",
            PhoneNumber = "0918887766",
            LoyaltyTier = "GOLD"
        };
        var wallet = new CustomerWallet
        {
            CustomerId = customerId,
            BalanceType = BalanceType.POINTS,
            CurrentBalance = 150
        };
        customer.Wallets.Add(wallet);

        var reward = new Reward
        {
            Title = "قسيمة زارا 25 دينار",
            PointsCost = 100,
            DiscountValue = 25.00m,
            IsActive = true,
            StockQuantity = 10,
            ValidityDays = 30
        };

        await context.Customers.AddAsync(customer);
        await context.CustomerWallets.AddAsync(wallet);
        await context.Rewards.AddAsync(reward);
        await context.SaveChangesAsync();

        var controller = new RewardsController(context, NullLogger<RewardsController>.Instance);
        SetControllerUser(controller, customerId);

        // Act
        var result = await controller.RedeemReward(reward.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RedemptionResponseDto>(okResult.Value);

        Assert.True(response.Success);
        Assert.StartsWith("MM-", response.RedemptionCode);
        Assert.Equal(100, response.PointsSpent);
        Assert.Equal(50, response.CurrentWalletPointsBalance); // 150 - 100 = 50

        // Verify DB
        var updatedWallet = await context.CustomerWallets.FirstAsync(w => w.CustomerId == customerId);
        Assert.Equal(50, updatedWallet.PointsBalance);

        var updatedReward = await context.Rewards.FirstAsync(r => r.Id == reward.Id);
        Assert.Equal(9, updatedReward.StockQuantity);

        var pointTx = await context.WalletLedger.Include(t => t.Wallet).FirstOrDefaultAsync(t => t.Wallet.CustomerId == customerId && t.TransactionType == WalletTransactionType.REDEEM_PURCHASE);
        Assert.NotNull(pointTx);
        Assert.Equal(-100, pointTx.AmountChanged);
        Assert.Equal(50, pointTx.BalanceAfter);
    }

    [Fact]
    public async Task RedeemReward_WithInsufficientPoints_ShouldReturnBadRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = "CUST-LOW-POINTS";
        var customer = new Customer
        {
            CustomerId = customerId,
            FullName = "ليلى كريم",
            PhoneNumber = "0915554433",
            LoyaltyTier = "BRONZE"
        };
        var wallet = new CustomerWallet
        {
            CustomerId = customerId,
            BalanceType = BalanceType.POINTS,
            CurrentBalance = 30
        };
        customer.Wallets.Add(wallet);

        var reward = new Reward
        {
            Title = "قسيمة 100 نقطة",
            PointsCost = 100,
            IsActive = true,
            ValidityDays = 30
        };

        await context.Customers.AddAsync(customer);
        await context.CustomerWallets.AddAsync(wallet);
        await context.Rewards.AddAsync(reward);
        await context.SaveChangesAsync();

        var controller = new RewardsController(context, NullLogger<RewardsController>.Instance);
        SetControllerUser(controller, customerId);

        // Act
        var result = await controller.RedeemReward(reward.Id);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task RedeemReward_WhenOutOfStock_ShouldReturnBadRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = "CUST-STOCK-TEST";
        var customer = new Customer
        {
            CustomerId = customerId,
            FullName = "عادل يونس",
            PhoneNumber = "0916667788",
            LoyaltyTier = "SILVER"
        };
        var wallet = new CustomerWallet
        {
            CustomerId = customerId,
            BalanceType = BalanceType.POINTS,
            CurrentBalance = 500
        };
        customer.Wallets.Add(wallet);

        var reward = new Reward
        {
            Title = "مكافأة منتهية الكمية",
            PointsCost = 50,
            IsActive = true,
            StockQuantity = 0,
            ValidityDays = 30
        };

        await context.Customers.AddAsync(customer);
        await context.CustomerWallets.AddAsync(wallet);
        await context.Rewards.AddAsync(reward);
        await context.SaveChangesAsync();

        var controller = new RewardsController(context, NullLogger<RewardsController>.Instance);
        SetControllerUser(controller, customerId);

        // Act
        var result = await controller.RedeemReward(reward.Id);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }
}

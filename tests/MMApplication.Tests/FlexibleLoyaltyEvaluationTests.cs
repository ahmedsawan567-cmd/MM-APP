using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class FlexibleLoyaltyEvaluationTests
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
    public async Task Mode1_FixedPerInvoice_ShouldAwardFixedPointsRegardlessOfSpend()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var rule = new LoyaltyEarningRule
        {
            RuleName = "مكافأة الزيارة الثابتة",
            CalculationType = "FIXED_PER_INVOICE",
            FixedPointsPerInvoice = 8,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await context.LoyaltyEarningRules.AddAsync(rule);
        await context.SaveChangesAsync();

        var service = new FlexibleLoyaltyEvaluationService(context);

        // Act
        var result = await service.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = 500.00m,
            CustomerTier = "BRONZE"
        });

        // Assert
        Assert.Equal(8, result.TotalPointsAwarded);
        Assert.Equal(8, result.BasePointsEarned);
        Assert.Equal(0, result.ItemBonusPointsEarned);
        Assert.Equal("FIXED_PER_INVOICE", result.CalculationType);
        Assert.Contains("\"calculationType\":\"FIXED_PER_INVOICE\"", result.PointsBreakdownJson);
    }

    [Fact]
    public async Task Mode2_SpendRatio_ShouldCalculatePointsBasedOnConfiguredRate()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var rule = new LoyaltyEarningRule
        {
            RuleName = "النقاط حسب المبلغ المزدوج",
            CalculationType = "SPEND_RATIO",
            SpendRatioRate = 0.20m, // 2 points per 10 LYD
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await context.LoyaltyEarningRules.AddAsync(rule);
        await context.SaveChangesAsync();

        var service = new FlexibleLoyaltyEvaluationService(context);

        // Act
        var result = await service.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = 150.00m, // 150 * 0.20 = 30 points
            CustomerTier = "BRONZE"
        });

        // Assert
        Assert.Equal(30, result.TotalPointsAwarded);
        Assert.Equal(30, result.BasePointsEarned);
        Assert.Equal("SPEND_RATIO", result.CalculationType);
    }

    [Fact]
    public async Task Mode3_ItemBooster_ShouldAwardBonusPointsForMatchingReceiptItems()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var rule = new LoyaltyEarningRule
        {
            RuleName = "قاعدة مكافأة الأصناف",
            CalculationType = "ITEM_BOOSTER",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var booster = new ProductBoosterRule
        {
            Title = "بونص عصير المراعي",
            BrandOrItemKeyword = "Almarai",
            BonusPoints = 25,
            PointsMultiplier = 1.0m,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5)
        };
        await context.LoyaltyEarningRules.AddAsync(rule);
        await context.ProductBoosterRules.AddAsync(booster);
        await context.SaveChangesAsync();

        var service = new FlexibleLoyaltyEvaluationService(context);

        // Act
        var result = await service.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = 80.00m,
            CustomerTier = "BRONZE",
            Items = new List<ExtractedReceiptItemDto>
            {
                new() { Description = "Almarai Fresh Juice 1L", TotalPrice = 10m },
                new() { Description = "Croissant", TotalPrice = 5m }
            }
        });

        // Assert
        Assert.Equal(25, result.TotalPointsAwarded);
        Assert.Equal(0, result.BasePointsEarned);
        Assert.Equal(25, result.ItemBonusPointsEarned);
        Assert.Single(result.MatchedBoosters);
        Assert.Equal("بونص عصير المراعي", result.MatchedBoosters[0].Title);
    }

    [Fact]
    public async Task Mode4_Hybrid_ShouldCombineVisitRewardSpendRatioSKUBoostersAndTierMultiplier()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var rule = new LoyaltyEarningRule
        {
            RuleName = "النظام الهجين الشامل",
            CalculationType = "HYBRID",
            FixedPointsPerInvoice = 5,
            SpendRatioRate = 0.10m, // 100 LYD * 0.10 = 10 points
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var booster = new ProductBoosterRule
        {
            Title = "بونص شوكولاتة كندر",
            BrandOrItemKeyword = "Kinder",
            BonusPoints = 15,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5)
        };
        await context.LoyaltyEarningRules.AddAsync(rule);
        await context.ProductBoosterRules.AddAsync(booster);
        await context.SaveChangesAsync();

        var service = new FlexibleLoyaltyEvaluationService(context);

        // Act: Evaluate for GOLD tier (1.5x multiplier on base points)
        // Base = Fixed (5) + Spend (10) = 15 points
        // Base * Tier Multiplier (1.5) = 15 * 1.5 = 22.5 => 23 or 22 points
        // Total = 23 + ItemBonus (15) = 38 points
        var result = await service.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = 100.00m,
            CustomerTier = "GOLD",
            Items = new List<ExtractedReceiptItemDto>
            {
                new() { Description = "Kinder Bueno 2-Pack", TotalPrice = 12m }
            }
        });

        // Assert
        Assert.True(result.TotalPointsAwarded >= 37);
        Assert.True(result.BasePointsEarned >= 22);
        Assert.Equal(15, result.ItemBonusPointsEarned);
        Assert.Equal("HYBRID", result.CalculationType);
    }

    [Fact]
    public async Task MaxPointsCap_ShouldEnforcePointsUpperLimit()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var rule = new LoyaltyEarningRule
        {
            RuleName = "قاعدة محددة السقف",
            CalculationType = "SPEND_RATIO",
            SpendRatioRate = 0.10m,
            MaxPointsCap = 50, // Max cap 50 points
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await context.LoyaltyEarningRules.AddAsync(rule);
        await context.SaveChangesAsync();

        var service = new FlexibleLoyaltyEvaluationService(context);

        // Act
        var result = await service.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = 2000.00m, // 2000 * 0.10 = 200 points, but capped at 50
            CustomerTier = "BRONZE"
        });

        // Assert
        Assert.Equal(50, result.TotalPointsAwarded);
        Assert.Contains("\"maxPointsCap\":50", result.PointsBreakdownJson);
    }

    [Fact]
    public async Task MinInvoiceAmount_WhenBelowThreshold_ShouldAwardZeroPoints()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var rule = new LoyaltyEarningRule
        {
            RuleName = "حد أدنى 50 دينار",
            CalculationType = "FIXED_PER_INVOICE",
            FixedPointsPerInvoice = 10,
            MinInvoiceAmount = 50.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await context.LoyaltyEarningRules.AddAsync(rule);
        await context.SaveChangesAsync();

        var service = new FlexibleLoyaltyEvaluationService(context);

        // Act
        var result = await service.EvaluateInvoicePointsAsync(new LoyaltyEvaluationRequest
        {
            TotalAmount = 25.00m, // Below 50.00m threshold
            CustomerTier = "BRONZE"
        });

        // Assert
        Assert.Equal(0, result.TotalPointsAwarded);
        Assert.Equal(0, result.BasePointsEarned);
    }

    [Fact]
    public async Task MobileHomeController_ShouldReturnServerDrivenHomeFeedWithTargetedBanners()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        
        var customer = new Customer
        {
            CustomerId = "CUST-GOLD-01",
            FullName = "أحمد الترهوني",
            PhoneNumber = "0912223344",
            LoyaltyTier = "GOLD",
            RegisteredAt = DateTime.UtcNow
        };
        var wallet = new CustomerWallet
        {
            CustomerId = customer.CustomerId,
            BalanceType = BalanceType.POINTS,
            CurrentBalance = 1850
        };
        var store = new Tenant
        {
            TenantId = "TEN-ZARA",
            NameAr = "زارا للأزياء",
            NameEn = "Zara",
            Category = "أزياء وموضة",
            UnitNumber = "G-12",
            IsActive = true
        };
        var bannerAll = new PromotionBanner
        {
            Id = Guid.NewGuid(),
            Title = "عروض نهاية الأسبوع",
            ImageUrl = "https://images.unsplash.com/banner1",
            ActionType = "OPEN_STORE",
            ActionPayloadJson = "{\"tenantId\":\"TEN-ZARA\"}",
            TargetAudience = "ALL",
            DisplayOrder = 1,
            IsActive = true
        };
        var bannerGold = new PromotionBanner
        {
            Id = Guid.NewGuid(),
            Title = "مكافأة حصرية للذهبي",
            ImageUrl = "https://images.unsplash.com/banner2",
            ActionType = "OPEN_COUPON",
            ActionPayloadJson = "{\"couponId\":\"COUPON-GOLD-50\"}",
            TargetAudience = "GOLD_ONLY",
            DisplayOrder = 2,
            IsActive = true
        };
        var bannerPlatinum = new PromotionBanner
        {
            Id = Guid.NewGuid(),
            Title = "خاص بالبلاتيني فقط",
            ImageUrl = "https://images.unsplash.com/banner3",
            ActionType = "EXTERNAL_URL",
            TargetAudience = "PLATINUM_ONLY",
            DisplayOrder = 3,
            IsActive = true
        };
        var booster = new ProductBoosterRule
        {
            Id = Guid.NewGuid(),
            Title = "بونص لافازا كوفي",
            BrandOrItemKeyword = "Lavazza",
            BonusPoints = 30,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10)
        };

        await context.Customers.AddAsync(customer);
        await context.CustomerWallets.AddAsync(wallet);
        await context.Tenants.AddAsync(store);
        await context.PromotionBanners.AddRangeAsync(bannerAll, bannerGold, bannerPlatinum);
        await context.ProductBoosterRules.AddAsync(booster);
        await context.SaveChangesAsync();

        var controller = new MobileHomeController(context, NullLogger<MobileHomeController>.Instance);

        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new(ClaimTypes.NameIdentifier, "CUST-GOLD-01"),
            new(ClaimTypes.Role, "Customer")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        // Act
        var response = await controller.GetHomeLayout();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(response);
        var homeDto = Assert.IsType<MobileHomeResponseDto>(okResult.Value);

        Assert.NotNull(homeDto.User);
        Assert.Equal("أحمد الترهوني", homeDto.User.FullName);
        Assert.Equal("GOLD", homeDto.User.LoyaltyTier);
        Assert.Equal(1850, homeDto.User.PointsBalance);
        Assert.Equal("VIP", homeDto.User.NextTierName); // من إعدادات العضويات الافتراضية

        // Targeted Banners: Gold user should receive "ALL" and "GOLD_ONLY", but NOT "PLATINUM_ONLY"
        Assert.Equal(2, homeDto.HeroBanners.Count);
        Assert.Contains(homeDto.HeroBanners, b => b.ActionType == "OPEN_STORE" && b.Title == "عروض نهاية الأسبوع");
        Assert.Contains(homeDto.HeroBanners, b => b.ActionType == "OPEN_COUPON" && b.Title == "مكافأة حصرية للذهبي");
        Assert.DoesNotContain(homeDto.HeroBanners, b => b.Title == "خاص بالبلاتيني فقط");

        // Boosters & Categories
        Assert.NotEmpty(homeDto.HotBoosters);
        Assert.Equal("بونص لافازا كوفي", homeDto.HotBoosters[0].Title);
        Assert.Equal(30, homeDto.HotBoosters[0].BonusPoints);

        Assert.NotEmpty(homeDto.QuickCategories);
        Assert.Contains(homeDto.QuickCategories, c => c.Key == "FASHION" && c.NameAr == "أزياء وملابس");
    }
}

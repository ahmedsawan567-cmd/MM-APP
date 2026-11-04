using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Api.Services;
using MMApplication.Api.ViewModels;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Tests;

public class RewardsAdminTests
{
    private static AppDbContext CreateDb(out Reward reward)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        reward = new Reward { Title = "قهوة مجانية", PointsCost = 40, DiscountValue = 12, ValidityDays = 30 };
        db.Rewards.Add(reward);
        db.Customers.Add(new Customer { CustomerId = "C1", FullName = "سارة", PhoneNumber = "0911", RegisteredAt = DateTime.UtcNow });
        db.Redemptions.Add(new RewardRedemption { CustomerId = "C1", RewardId = reward.Id, RedemptionCode = "MM-ACTIVE", PointsSpent = 40, ExpiresAt = DateTime.UtcNow.AddDays(5) });
        db.Redemptions.Add(new RewardRedemption { CustomerId = "C1", RewardId = reward.Id, RedemptionCode = "MM-OLD", PointsSpent = 40, ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        db.SaveChanges();
        return db;
    }

    private static AdminDashboardController Controller(AppDbContext db, string role, string systemRole) =>
        new(db, NullLogger<AdminDashboardController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "U1"),
                        new Claim(ClaimTypes.Name, "موظف"),
                        new Claim(ClaimTypes.Role, role),
                        new Claim("SystemRole", systemRole),
                        new Claim("TenantName", "كافي كادي"),
                        new Claim("TenantId", systemRole == "STORE_MANAGER" ? "TEN-KADI" : ""),
                    }, "Test")),
                },
            },
        };

    [Fact]
    public async Task Voucher_CanBeUsedOnce()
    {
        using var db = CreateDb(out _);
        var service = new VoucherService(db);

        var first = await service.UseAsync(" mm-active ", "كافي كادي", "U1");
        var second = await service.UseAsync("MM-ACTIVE", "كافي كادي", "U1");

        Assert.True(first.Success);
        Assert.Equal("USED", first.Voucher!.Status);
        Assert.False(second.Success);
        Assert.Contains("مسبقاً", second.Message);
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "VOUCHER_USE").ToListAsync());
    }

    [Fact]
    public async Task ExpiredVoucher_IsRejected_AndMarkedExpired()
    {
        using var db = CreateDb(out _);
        var result = await new VoucherService(db).UseAsync("MM-OLD", "x", "U1");

        Assert.False(result.Success);
        Assert.Equal("EXPIRED", result.Voucher!.Status);
        Assert.Equal(RedemptionStatus.EXPIRED, (await db.Redemptions.FirstAsync(r => r.RedemptionCode == "MM-OLD")).Status);
    }

    [Fact]
    public async Task UnknownVoucher_Returns404()
    {
        using var db = CreateDb(out _);
        var result = await new VoucherService(db).LookupAsync("MM-NOPE");
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task StoreManager_UsesVoucher_RecordedUnderTheirStore()
    {
        using var db = CreateDb(out _);
        var result = await Controller(db, "storemanager", "STORE_MANAGER").VoucherUse(new ReviewDecisionRequest { Reason = "MM-ACTIVE" });

        Assert.Equal(200, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode ?? 200);
        Assert.Equal("كافي كادي", (await db.Redemptions.FirstAsync(r => r.RedemptionCode == "MM-ACTIVE")).UsedByMerchant);
    }

    [Fact]
    public async Task SuperAdmin_CreatesAndTogglesReward()
    {
        using var db = CreateDb(out _);
        var controller = Controller(db, "superadmin", "SUPER_ADMIN");

        var save = await controller.RewardSave(new RewardEditRequest { Title = "خصم 50", PointsCost = 150, DiscountValue = 50, ValidityDays = 14, StockQuantity = 10 });
        Assert.IsType<JsonResult>(save);
        var created = await db.Rewards.FirstAsync(r => r.Title == "خصم 50");
        Assert.Equal(10, created.StockQuantity);

        await controller.RewardToggle(created.Id);
        Assert.False((await db.Rewards.AsNoTracking().FirstAsync(r => r.Id == created.Id)).IsActive);
    }

    [Fact]
    public async Task InvalidReward_IsRefused()
    {
        using var db = CreateDb(out _);
        var result = await Controller(db, "superadmin", "SUPER_ADMIN").RewardSave(new RewardEditRequest { Title = "", PointsCost = 0, ValidityDays = 0 });
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StoreManager_CannotEditRewards()
    {
        using var db = CreateDb(out var reward);
        var result = await Controller(db, "storemanager", "STORE_MANAGER").RewardToggle(reward.Id);
        Assert.IsType<ForbidResult>(result);
    }
}

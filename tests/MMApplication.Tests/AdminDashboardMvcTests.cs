using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Api.ViewModels;
using MMApplication.Application.DTOs;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class AdminDashboardMvcTests
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
    public async Task Index_ShouldReturnOverviewModelForRazorView()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SuperAdminOverviewDto>(viewResult.Model);

        Assert.True(model.TotalSalesVolume > 0);
        Assert.NotEmpty(model.ScanActivityPace);
        Assert.NotEmpty(model.CategoryDistribution);
        Assert.NotEmpty(model.TopPerformingStores);
        Assert.Equal("overview", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Stores_ShouldReturnStoresListForRazorView()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Stores(null, null);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<StoreTenantDto>>(viewResult.Model);

        Assert.NotEmpty(model);
        Assert.Equal("stores", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Fraud_ShouldReturnFraudPageViewModel()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Fraud("ALL");
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<FraudPageViewModel>(viewResult.Model);

        Assert.Equal("ALL", model.StatusFilter);
        Assert.Equal("fraud", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Ads_ShouldReturnBannersList()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Ads();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<PromotionBannerDto>>(viewResult.Model);

        Assert.NotEmpty(model);
        Assert.Equal("ads", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Loyalty_ShouldReturnLoyaltyPageViewModel()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Loyalty();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoyaltyPageViewModel>(viewResult.Model);

        Assert.NotEmpty(model.Campaigns);
        Assert.NotNull(model.Rules);
        Assert.Equal("loyalty", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Settlements_ShouldReturnSettlementsList()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Settlements();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<StoreSettlementDto>>(viewResult.Model);

        Assert.NotEmpty(model);
        Assert.Equal("settlements", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Customers_ShouldReturnCustomersList()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Customers("ALL", null, null, null);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<SuperAdminCustomerDto>>(viewResult.Model);

        Assert.NotEmpty(model);
        Assert.Equal("customers", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Staff_ShouldReturnStaffPageViewModel()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Staff();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StaffPageViewModel>(viewResult.Model);

        Assert.NotEmpty(model.StaffMembers);
        Assert.NotEmpty(model.Stores);
        Assert.Equal("staff", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task Settings_ShouldReturnSettingsPageViewModel()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = await controller.Settings(null);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsPageViewModel>(viewResult.Model);

        Assert.NotNull(model.Settings);
        Assert.NotEmpty(model.AuditLogs);
        Assert.Equal("settings", controller.ViewData["ActiveTab"]);
    }

    [Fact]
    public async Task SeedData_ShouldContainRequestedUsersAndStores()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        // 1. Verify Users
        var programmer = await context.SystemUsers.FirstOrDefaultAsync(u => u.UserId == "programer");
        Assert.NotNull(programmer);
        Assert.Equal(SystemUserRole.SUPER_ADMIN, programmer.Role);

        var mostafa = await context.SystemUsers.FirstOrDefaultAsync(u => u.UserId == "USR-MOSTAFA" || u.FullName.Contains("مصطفى صوان"));
        Assert.NotNull(mostafa);
        Assert.Equal(SystemUserRole.SUPER_ADMIN, mostafa.Role);

        var mourad = await context.SystemUsers.FirstOrDefaultAsync(u => u.UserId == "USR-MOURAD" || u.FullName.Contains("مراد"));
        Assert.NotNull(mourad);
        Assert.Equal(SystemUserRole.FRAUD_AUDITOR, mourad.Role);

        // 2. Verify Stores
        var cleaning = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-CLEANING");
        Assert.NotNull(cleaning);

        var grocery = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-GROCERY");
        Assert.NotNull(grocery);

        var eveningDress = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-EVENING-DRESS");
        Assert.NotNull(eveningDress);

        var kadiCafe = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-KADI-CAFE");
        Assert.NotNull(kadiCafe);

        var womenFashion = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-WOMEN-FASHION");
        Assert.NotNull(womenFashion);

        var kidsWear = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-KIDS-WEAR");
        Assert.NotNull(kidsWear);

        var chocolate = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-CHOCOLATE");
        Assert.NotNull(chocolate);

        var royalTouch = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-ROYAL-TOUCH");
        Assert.NotNull(royalTouch);

        var saqrGames = await context.Tenants.FirstOrDefaultAsync(t => t.TenantId == "TEN-SAQR-GAMES");
        Assert.NotNull(saqrGames);

        // 3. Verify Aliases
        var aliases = await context.StoreAliases.ToListAsync();
        Assert.NotEmpty(aliases);
        Assert.Contains(aliases, a => a.TenantId == "TEN-KADI-CAFE" && a.AliasName.Contains("كادي"));
    }

    [Fact]
    public void Login_Get_ShouldReturnLoginViewModel()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);

        var result = controller.Login("/AdminDashboard/Index");
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginViewModel>(viewResult.Model);

        Assert.Equal("/AdminDashboard/Index", model.ReturnUrl);
    }

    [Fact]
    public async Task StoreManager_Stores_ShouldOnlyReturnAssignedStore()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "USR-KADI-MGR"),
            new(System.Security.Claims.ClaimTypes.Name, "أحمد القمودي"),
            new(System.Security.Claims.ClaimTypes.Role, "storemanager"),
            new("SystemRole", "STORE_MANAGER"),
            new("TenantId", "TEN-KADI-CAFE"),
            new("TenantName", "كافي كادي")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };

        var result = await controller.Stores(null, null);
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<StoreTenantDto>>(viewResult.Model);

        Assert.Single(model);
        Assert.Equal("TEN-KADI-CAFE", model[0].Id);
    }

    [Fact]
    public async Task StoreManager_Staff_ShouldRedirectToOverview()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        await DataSeeder.SeedAsync(context, hasher);

        var controller = new AdminDashboardController(context, NullLogger<AdminDashboardController>.Instance);
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "USR-KADI-MGR"),
            new(System.Security.Claims.ClaimTypes.Name, "أحمد القمودي"),
            new(System.Security.Claims.ClaimTypes.Role, "storemanager"),
            new("SystemRole", "STORE_MANAGER"),
            new("TenantId", "TEN-KADI-CAFE")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };

        var result = await controller.Staff();
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
    }
}



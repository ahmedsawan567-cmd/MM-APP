using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MMApplication.Tests;

/// <summary>إدارة حسابات طاقم العمل: المدير العام فقط، مع حماية آخر مدير عام والحساب الحالي.</summary>
public class StaffManagementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StaffManagementTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "StaffTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "InMemory",
                ["InMemoryDbName"] = dbName,
                ["SeedData"] = "Demo"
            }));
        });
    }

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/AdminDashboard/Login");
        var token = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;
        var resp = await client.PostAsync("/AdminDashboard/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Username"] = username,
            ["Password"] = password
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.DoesNotContain("Login", resp.Headers.Location?.ToString() ?? "");
        return client;
    }

    private static object NewStaff(string phone, string role = "Auditor", string? store = null, string password = "Strong-Pass9") =>
        new { fullName = "موظف اختبار", phoneNumber = phone, password, staffRole = role, storeTenantId = store };

    [Fact]
    public async Task StoreManager_CannotCreateStaff()
    {
        var client = await LoginAsync("USR-KADI-MGR", "Manager123!");
        var resp = await client.PostAsJsonAsync("/api/superadmin/staff", NewStaff("0920000001", "SuperAdmin"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Auditor_CannotListOrDeleteStaff()
    {
        var client = await LoginAsync("USR-MOURAD", "Auditor123!");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/superadmin/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/superadmin/staff/USR-MOSTAFA")).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CreatesStaff_WhoCanLoginWithPhone()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        var resp = await admin.PostAsJsonAsync("/api/superadmin/staff", NewStaff("0920000002"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await LoginAsync("0920000002", "Strong-Pass9");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task CreateStaff_RejectsWeakPassword(string password)
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        var resp = await admin.PostAsJsonAsync("/api/superadmin/staff", NewStaff("0920000003", password: password));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_CashierRequiresStore()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        var resp = await admin.PostAsJsonAsync("/api/superadmin/staff", NewStaff("0920000004", "Cashier"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CannotDeleteOrDisableOwnAccount()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync("/api/superadmin/staff/USR-MOSTAFA")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/superadmin/staff/USR-MOSTAFA/toggle-active", null)).StatusCode);
        var demote = await admin.PutAsJsonAsync("/api/superadmin/staff/USR-MOSTAFA",
            new { fullName = "x", phoneNumber = "", staffRole = "Auditor" });
        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
    }

    [Fact]
    public async Task DisabledStaff_CannotLogin()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/superadmin/staff", NewStaff("0920000005"))).StatusCode);
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/superadmin/staff");
        var id = list.EnumerateArray().First(s => s.GetProperty("phoneNumber").GetString() == "0920000005").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/superadmin/staff/{id}/toggle-active", null)).StatusCode);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/AdminDashboard/Login");
        var token = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;
        var resp = await client.PostAsync("/AdminDashboard/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Username"] = "0920000005", ["Password"] = "Strong-Pass9"
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode); // يبقى في صفحة الدخول مع رسالة "الحساب معطل"
    }

    [Fact]
    public async Task HealthEndpoint_IsPublic()
    {
        var resp = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("ok", resp.GetProperty("status").GetString());
    }
}

public class AdminPasswordResetTests
{
    [Fact]
    public async Task Reset_SetsNewWorkingPassword_AndReactivatesAdmin()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<MMApplication.Infrastructure.Data.AppDbContext>()
            .UseInMemoryDatabase("ResetAdmin_" + Guid.NewGuid()).Options;
        await using var db = new MMApplication.Infrastructure.Data.AppDbContext(options);
        var hasher = new MMApplication.Infrastructure.Services.PasswordHasherService();

        Assert.NotNull(await MMApplication.Infrastructure.Data.DataSeeder.EnsureBootstrapAdminAsync(db, hasher, null));
        var admin = db.SystemUsers.Single(u => u.UserId == "admin");
        admin.IsActive = false;
        await db.SaveChangesAsync();

        var password = await MMApplication.Infrastructure.Data.DataSeeder.ResetAdminPasswordAsync(db, hasher, null);

        admin = db.SystemUsers.Single(u => u.UserId == "admin");
        Assert.True(admin.IsActive);
        Assert.True(hasher.VerifyPassword(password, admin.PasswordHash));
        Assert.Contains(db.AuditLogs, a => a.Action == "ADMIN_PASSWORD_RESET");
    }
}

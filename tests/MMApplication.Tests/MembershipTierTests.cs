using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Tests;

public class MembershipTierLogicTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<AppDbContext> SeedAsync()
    {
        var db = NewDb();
        await MembershipTiers.EnsureDefaultsAsync(db);
        db.Customers.Add(new Customer { CustomerId = "C1", FullName = "سارة", PhoneNumber = "0910000001", LoyaltyTier = "BRONZE" });
        await db.SaveChangesAsync();
        return db;
    }

    private static void Invoice(AppDbContext db, int points, decimal amount) => db.Invoices.Add(new Invoice
    {
        CustomerId = "C1", InvoiceNumber = Guid.NewGuid().ToString("N")[..8], TotalAmount = amount, PointsAwarded = points,
        ProcessingStatus = InvoiceStatus.APPROVED,
    });

    [Fact]
    public async Task Customer_IsUpgraded_WhenConditionsMet_AndNotified()
    {
        await using var db = await SeedAsync();
        Invoice(db, 300, 100);
        await db.SaveChangesAsync();
        Assert.Null(await MembershipTiers.EvaluateAsync(db, "C1"));

        Invoice(db, 1300, 400);
        await db.SaveChangesAsync();
        Assert.Equal("GOLD", await MembershipTiers.EvaluateAsync(db, "C1")); // يقفز مباشرة لأعلى مستوى مستحق
        Assert.Equal("GOLD", db.Customers.Single().LoyaltyTier);
        Assert.Contains(db.Notifications, n => n.RecipientId == "C1" && n.Type == "TIER_UPGRADE");
        Assert.Contains(db.AuditLogs, a => a.Action == "CUSTOMER_TIER_CHANGE");
    }

    [Fact]
    public async Task LockedTier_NeverChangesAutomatically_AndNoAutoDowngrade()
    {
        await using var db = await SeedAsync();
        var c = db.Customers.Single();
        c.TierLocked = true;
        Invoice(db, 6000, 9000);
        await db.SaveChangesAsync();
        Assert.Null(await MembershipTiers.EvaluateAsync(db, "C1"));

        c.TierLocked = false;
        c.LoyaltyTier = "VIP";
        await db.SaveChangesAsync();
        db.Invoices.RemoveRange(db.Invoices);
        await db.SaveChangesAsync();
        Assert.Null(await MembershipTiers.EvaluateAsync(db, "C1"));
        Assert.Equal("VIP", db.Customers.Single().LoyaltyTier);
    }

    [Fact]
    public async Task Progress_And_Multiplier_ComeFromTierSettings()
    {
        await using var db = await SeedAsync();
        var silver = db.MembershipTiers.Single(t => t.Code == "SILVER");
        silver.MinSpend = 1000;
        silver.PointsMultiplier = 1.4m;
        Invoice(db, 500, 250);
        await db.SaveChangesAsync();

        var p = await MembershipTiers.ProgressAsync(db, db.Customers.Single());
        Assert.Equal("SILVER", p.Next!.Code);
        Assert.Equal(0, p.PointsToNext);
        Assert.Equal(750, p.SpendToNext);
        Assert.Equal(25, p.Percent);
        Assert.Equal(1.4m, await MembershipTiers.MultiplierAsync(db, "SILVER"));
        Assert.Equal(1m, await MembershipTiers.MultiplierAsync(db, "UNKNOWN"));
    }
}

public class MembershipTierPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MembershipTierPageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "InMemory",
                ["InMemoryDbName"] = "TiersDb_" + Guid.NewGuid(),
            }));
        });
    }

    [Fact]
    public async Task Admin_CreatesEditsAndDeletesTier_MovingMembers()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/AdminDashboard/Login");
        string Token(string h) => Regex.Match(h, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;
        await client.PostAsync("/AdminDashboard/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Token(html), ["Username"] = "USR-MOSTAFA", ["Password"] = "Admin123!" }));

        var page = await client.GetStringAsync("/AdminDashboard/Tiers");
        Assert.Contains("ذهبي", page);
        var af = Token(page);

        HttpRequestMessage Post(string url, HttpContent content)
        {
            var r = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            r.Headers.Add("RequestVerificationToken", af);
            return r;
        }

        var create = await client.SendAsync(Post("/AdminDashboard/TierSave", JsonContent.Create(new
        {
            code = "PLATINUM", nameAr = "بلاتيني", color = "#334155", sortOrder = 5, minPoints = 10000, pointsMultiplier = 2, isActive = true, benefits = "ضعف النقاط",
        })));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Post("/AdminDashboard/TierSave", JsonContent.Create(new { code = "PLATINUM", nameAr = "مكرر" })))).StatusCode);

        // نقل عملاء VIP عند حذفها
        using (var scope = _factory.Services.CreateScope())
            Assert.True(scope.ServiceProvider.GetRequiredService<AppDbContext>().Customers.Any(c => c.LoyaltyTier == "VIP"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Post("/AdminDashboard/TierDelete", new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "VIP" })))).StatusCode);
        var del = await client.SendAsync(Post("/AdminDashboard/TierDelete", new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "VIP", ["moveTo"] = "PLATINUM" })));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(db.Customers.Any(c => c.LoyaltyTier == "VIP"));
            Assert.True(db.Customers.Any(c => c.LoyaltyTier == "PLATINUM"));
        }
        Assert.Contains("بلاتيني", await client.GetStringAsync("/AdminDashboard/Customers?tier=PLATINUM"));
    }
}

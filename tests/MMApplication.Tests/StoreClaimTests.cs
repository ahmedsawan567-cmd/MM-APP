using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Tests;

/// <summary>كسب النقاط بباركود المتجر أو بطاقة NFC: طلب من العميل يؤكده كاشير المتجر.</summary>
public class StoreClaimTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StoreClaimTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "InMemory",
                ["InMemoryDbName"] = "ClaimsDb_" + Guid.NewGuid(),
            }));
        });
    }

    private static string Token(string html) =>
        Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;

    private async Task<HttpClient> StaffAsync(string user, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/AdminDashboard/Login");
        await client.PostAsync("/AdminDashboard/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Token(html), ["Username"] = user, ["Password"] = password }));
        return client;
    }

    private async Task<HttpClient> CustomerAsync(string phone, string name = "عميل الباركود")
    {
        var app = _factory.CreateClient();
        var reg = await app.PostAsJsonAsync("/api/Auth/register", new { fullName = name, phoneNumber = phone, password = "Secret123!" });
        var token = (await reg.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        app.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return app;
    }

    private string CodeOf(string tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Tenants.Single(t => t.TenantId == tenantId).StoreCode!;
    }

    [Fact]
    public async Task Scan_AddsStorePointsInstantly_WithDailyLimit()
    {
        var admin = await StaffAsync("USR-MOSTAFA", "Admin123!");
        // المول يحدد نقاط الباركود للمتجر: 25 نقطة، مرتين يومياً
        var stores = await admin.GetFromJsonAsync<JsonElement>("/api/superadmin/stores");
        var zara = stores.EnumerateArray().First(x => x.GetProperty("id").GetString() == "TEN-ZARA");
        var put = await admin.PutAsJsonAsync("/api/superadmin/stores/TEN-ZARA", new
        {
            name = zara.GetProperty("name").GetString(), category = zara.GetProperty("category").GetString(), commissionRate = 5,
            isActive = true, scanPoints = 25, scanDailyLimit = 2,
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var app = await CustomerAsync("0926100200");
        var payload = StoreCodes.Payload(CodeOf("TEN-ZARA"));
        var store = await app.GetFromJsonAsync<JsonElement>($"/api/v1/purchases/store?code={Uri.EscapeDataString(payload)}");
        Assert.Equal(25, store.GetProperty("points").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await app.GetAsync("/api/v1/purchases/store?code=MMALL-STORE:WRONGCODE")).StatusCode);

        var first = await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, method = "QR" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, body.GetProperty("points").GetInt32());
        Assert.Equal(25, body.GetProperty("newBalance").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, method = "NFC" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, method = "QR" })).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var c = db.Customers.Single(x => x.PhoneNumber == "0926100200");
            Assert.Equal(50, db.CustomerWallets.Single(w => w.CustomerId == c.CustomerId && w.BalanceType == BalanceType.POINTS).CurrentBalance);
            Assert.Equal(2, db.WalletLedger.Count(l => l.Wallet.CustomerId == c.CustomerId));
        }

        // كاشير زارا يرى السجل ويلغي مسحاً مشبوهاً، وكاشير متجر آخر لا يراه
        var other = await StaffAsync("USR-KADI-MGR", "Manager123!");
        Assert.DoesNotContain("عميل الباركود", await other.GetStringAsync("/AdminDashboard/StoreClaims"));
        var cashier = await StaffAsync("USR-ZARA-CSH", "Merchant123!");
        var page = await cashier.GetStringAsync("/AdminDashboard/StoreClaims");
        Assert.Contains("عميل الباركود", page);
        var id = body.GetProperty("invoiceId").GetGuid();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/AdminDashboard/ClaimVoid?id={id}") { Content = JsonContent.Create(new { reason = "لم يكن في المحل" }) };
        req.Headers.Add("RequestVerificationToken", Token(page));
        Assert.Equal(HttpStatusCode.OK, (await cashier.SendAsync(req)).StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var c = db.Customers.Single(x => x.PhoneNumber == "0926100200");
            Assert.Equal(25, db.CustomerWallets.Single(w => w.CustomerId == c.CustomerId && w.BalanceType == BalanceType.POINTS).CurrentBalance);
        }
    }

    [Fact]
    public async Task DisabledStore_AndPrintPage()
    {
        var admin = await StaffAsync("USR-MOSTAFA", "Admin123!");
        var stores = await admin.GetFromJsonAsync<JsonElement>("/api/superadmin/stores");
        var zara = stores.EnumerateArray().First(x => x.GetProperty("id").GetString() == "TEN-ZARA");
        await admin.PutAsJsonAsync("/api/superadmin/stores/TEN-ZARA", new
        {
            name = zara.GetProperty("name").GetString(), category = zara.GetProperty("category").GetString(), commissionRate = 5, isActive = true, scanPoints = 0,
        });
        var app = await CustomerAsync("0926100300");
        var payload = StoreCodes.Payload(CodeOf("TEN-ZARA"));
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload })).StatusCode);

        var print = await admin.GetStringAsync("/AdminDashboard/StoreCodePrint/TEN-ZARA");
        Assert.Contains(payload, print);
        Assert.Contains("امسح واكسب", print);
    }

    [Fact]
    public async Task StoreOwner_ControlsOwnBarcode_AndSeesOwnCustomers()
    {
        var mgr = await StaffAsync("USR-KADI-MGR", "Manager123!");
        var page = await mgr.GetStringAsync("/AdminDashboard/StoreBarcode");
        Assert.Contains("باركود متجري وعملائي", page);

        // يحدد نقاط متجره
        var req = new HttpRequestMessage(HttpMethod.Post, "/AdminDashboard/StoreScanSettings?id=TEN-KADI-CAFE") { Content = JsonContent.Create(new { points = 40, dailyLimit = 2 }) };
        req.Headers.Add("RequestVerificationToken", Token(page));
        Assert.Equal(HttpStatusCode.OK, (await mgr.SendAsync(req)).StatusCode);
        // ولا يستطيع تعديل متجر آخر
        var other = new HttpRequestMessage(HttpMethod.Post, "/AdminDashboard/StoreScanSettings?id=TEN-ZARA") { Content = JsonContent.Create(new { points = 900, dailyLimit = 20 }) };
        other.Headers.Add("RequestVerificationToken", Token(page));
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.SendAsync(other)).StatusCode);

        // عميل يمسح باركود كادي فيأخذ 40 نقطة ويظهر في "عملاء الباركود"
        var app = await CustomerAsync("0926100400", "زبون كادي");
        var claim = await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = StoreCodes.Payload(CodeOf("TEN-KADI-CAFE")) });
        Assert.Equal(40, (await claim.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("points").GetInt32());

        var scans = await mgr.GetStringAsync("/AdminDashboard/StoreBarcode?tab=scans");
        Assert.Contains("زبون كادي", scans);
        Assert.DoesNotContain("0926100400", scans); // الهاتف مخفي جزئياً لصاحب المتجر
        var invoices = await mgr.GetStringAsync("/AdminDashboard/StoreBarcode?tab=invoices");
        Assert.DoesNotContain("زبون كادي", invoices);
        var csv = await mgr.GetAsync("/AdminDashboard/StoreBarcode?tab=scans&export=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
    }
}

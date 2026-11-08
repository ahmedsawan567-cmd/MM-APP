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

    private async Task<HttpClient> CustomerAsync(string phone)
    {
        var app = _factory.CreateClient();
        var reg = await app.PostAsJsonAsync("/api/Auth/register", new { fullName = "عميل الباركود", phoneNumber = phone, password = "Secret123!" });
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
    public async Task Customer_ScansStoreCode_CashierConfirms_PointsAdded()
    {
        var app = await CustomerAsync("0926100200");
        var payload = StoreCodes.Payload(CodeOf("TEN-ZARA"));

        var store = await app.GetFromJsonAsync<JsonElement>($"/api/v1/purchases/store?code={Uri.EscapeDataString(payload)}");
        Assert.Equal("TEN-ZARA", store.GetProperty("store").GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await app.GetAsync("/api/v1/purchases/store?code=MMALL-STORE:WRONGCODE")).StatusCode);

        var claim = await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, amount = 250, method = "NFC" });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var invoiceId = (await claim.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetGuid();
        // طلب ثانٍ قبل التأكيد مرفوض
        Assert.Equal(HttpStatusCode.Conflict, (await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, amount = 90 })).StatusCode);

        // كاشير متجر آخر لا يرى ولا يؤكد الطلب
        var otherStore = await StaffAsync("USR-KADI-MGR", "Manager123!");
        var otherPage = await otherStore.GetStringAsync("/AdminDashboard/StoreClaims");
        Assert.DoesNotContain("عميل الباركود", otherPage);

        // كاشير زارا يؤكد مع تصحيح المبلغ
        var cashier = await StaffAsync("USR-ZARA-CSH", "Merchant123!");
        var page = await cashier.GetStringAsync("/AdminDashboard/StoreClaims");
        Assert.Contains("عميل الباركود", page);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/AdminDashboard/ClaimConfirm?id={invoiceId}") { Content = JsonContent.Create(new { amount = 200 }) };
        req.Headers.Add("RequestVerificationToken", Token(page));
        var confirm = await cashier.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = db.Invoices.Single(i => i.InvoiceId == invoiceId);
        Assert.Equal(InvoiceStatus.APPROVED, inv.ProcessingStatus);
        Assert.Equal(200m, inv.TotalAmount);
        Assert.Equal("NFC", inv.Source);
        Assert.True(inv.PointsAwarded > 0);
        Assert.Contains(db.Notifications, n => n.RecipientId == inv.CustomerId && n.Type == "INVOICE_APPROVED");
    }

    [Fact]
    public async Task Claims_AreLimited_AndRejectable()
    {
        var app = await CustomerAsync("0926100300");
        var payload = StoreCodes.Payload(CodeOf("TEN-ZARA"));
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, amount = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, amount = 50000 })).StatusCode);

        var cashier = await StaffAsync("USR-ZARA-CSH", "Merchant123!");
        for (var i = 0; i < 3; i++)
        {
            var c = await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, amount = 40 + i });
            Assert.Equal(HttpStatusCode.OK, c.StatusCode);
            var id = (await c.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetGuid();
            var page = await cashier.GetStringAsync("/AdminDashboard/StoreClaims");
            var req = new HttpRequestMessage(HttpMethod.Post, $"/AdminDashboard/ClaimReject?id={id}") { Content = JsonContent.Create(new { reason = "لا توجد عملية" }) };
            req.Headers.Add("RequestVerificationToken", Token(page));
            Assert.Equal(HttpStatusCode.OK, (await cashier.SendAsync(req)).StatusCode);
        }
        // الحد اليومي (3 طلبات لكل متجر)
        Assert.Equal(HttpStatusCode.TooManyRequests, (await app.PostAsJsonAsync("/api/v1/purchases/claim", new { code = payload, amount = 30 })).StatusCode);

        // صفحة طباعة الباركود
        var admin = await StaffAsync("USR-MOSTAFA", "Admin123!");
        var print = await admin.GetStringAsync("/AdminDashboard/StoreCodePrint/TEN-ZARA");
        Assert.Contains(payload, print);
    }
}

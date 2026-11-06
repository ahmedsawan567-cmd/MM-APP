using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Tests;

/// <summary>صلاحيات الأدوار على المنصة وواجهات /api/superadmin، وقفل الدخول، وصفحات العملاء الجديدة.</summary>
public class PermissionsHardeningTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PermissionsHardeningTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "InMemory",
                ["InMemoryDbName"] = "PermsDb_" + Guid.NewGuid(),
            }));
        });
    }

    private static string Token(string html) =>
        Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;

    private async Task<(HttpClient Client, HttpResponseMessage Login)> LoginAsync(string user, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/AdminDashboard/Login");
        var res = await client.PostAsync("/AdminDashboard/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Token(html), ["Username"] = user, ["Password"] = password }));
        return (client, res);
    }

    private async Task<HttpClient> SignedInAsync(string user, string password)
    {
        var (client, res) = await LoginAsync(user, password);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        return client;
    }

    [Fact]
    public async Task StoreManager_CannotUseMallWideApis()
    {
        var mgr = await SignedInAsync("USR-KADI-MGR", "Manager123!");

        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.GetAsync("/api/superadmin/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.GetAsync("/api/superadmin/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.GetAsync("/api/superadmin/settlements")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.PostAsync($"/api/superadmin/settlements/{Guid.NewGuid()}/settle", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.PostAsync($"/api/superadmin/invoices/{Guid.NewGuid()}/void", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.GetAsync("/api/superadmin/banners")).StatusCode);

        // صفحات العملاء محجوبة عن مدير المتجر
        Assert.Equal(HttpStatusCode.Redirect, (await mgr.GetAsync("/AdminDashboard/Customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await mgr.GetAsync("/AdminDashboard/Fraud")).StatusCode);
    }

    [Fact]
    public async Task StoreManager_ManagesOnlyOwnStoreEarningRules()
    {
        var mgr = await SignedInAsync("USR-KADI-MGR", "Manager123!");

        var rules = await mgr.GetFromJsonAsync<JsonElement>("/api/superadmin/loyalty/earning-rules");
        Assert.All(rules.EnumerateArray(), r => Assert.Equal("TEN-KADI-CAFE", r.GetProperty("storeTenantId").GetString()));

        // يحاول إنشاء قاعدة لمتجر آخر: تُنشأ لمتجره هو
        var created = await mgr.PostAsJsonAsync("/api/superadmin/loyalty/earning-rules", new
        {
            storeTenantId = "TEN-ZARA", ruleName = "قاعدة تجربة", calculationType = "FIXED_PER_INVOICE", fixedPointsPerInvoice = 3,
        });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("TEN-KADI-CAFE", db.LoyaltyEarningRules.Single(r => r.RuleName == "قاعدة تجربة").StoreTenantId);
        }

        // ولا يستطيع تعديل القاعدة العامة للمول
        int mallRuleId;
        using (var scope = _factory.Services.CreateScope())
            mallRuleId = scope.ServiceProvider.GetRequiredService<AppDbContext>().LoyaltyEarningRules.First(r => r.StoreTenantId == null).RuleId;
        var edit = await mgr.PutAsJsonAsync($"/api/superadmin/loyalty/earning-rules/{mallRuleId}", new { ruleName = "x", calculationType = "SPEND_RATIO" });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
    }

    [Fact]
    public async Task Cashier_SeesOnlyOwnStore_AndNoManagementPages()
    {
        var cashier = await SignedInAsync("USR-ZARA-CSH", "Merchant123!");

        Assert.Equal(HttpStatusCode.Redirect, (await cashier.GetAsync("/AdminDashboard/Settlements")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await cashier.GetAsync("/AdminDashboard/Loyalty")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await cashier.GetAsync("/AdminDashboard/Stores")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await cashier.GetAsync("/AdminDashboard/Customers")).StatusCode);

        var invoices = await cashier.GetStringAsync("/AdminDashboard/Invoices");
        Assert.DoesNotContain("كافي كادي", invoices);

        // التحقق من القسائم متاح له
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/AdminDashboard/Rewards")).StatusCode);
    }

    [Fact]
    public async Task Auditor_ServesCustomers_ButCannotChangePointsOrStaff()
    {
        var auditor = await SignedInAsync("USR-MOURAD", "Auditor123!");
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/superadmin/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/AdminDashboard/CustomerDetails/CUST-002")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync("/api/superadmin/customers/CUST-002/adjust-points", new { pointsDelta = 10, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsync($"/api/superadmin/settlements/{Guid.NewGuid()}/settle", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await auditor.GetAsync("/AdminDashboard/Staff")).StatusCode);
    }

    [Fact]
    public async Task RepeatedWrongPasswords_LockAccount_UntilAdminResetsPassword()
    {
        for (var i = 0; i < 5; i++) await LoginAsync("USR-KADI-MGR", "wrong-password");

        // حتى كلمة المرور الصحيحة مرفوضة أثناء القفل
        var (_, locked) = await LoginAsync("USR-KADI-MGR", "Manager123!");
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Contains("مقفل مؤقتاً", await locked.Content.ReadAsStringAsync());

        var admin = await SignedInAsync("USR-MOSTAFA", "Admin123!");
        var staffPage = await admin.GetStringAsync("/AdminDashboard/Staff");
        Assert.Contains("مقفل مؤقتاً", staffPage);

        string mgrId;
        using (var scope = _factory.Services.CreateScope())
            mgrId = scope.ServiceProvider.GetRequiredService<AppDbContext>().SystemUsers.Single(u => u.UserId == "USR-KADI-MGR").UserId;
        var reset = await admin.PostAsync($"/api/superadmin/staff/{mgrId}/reset-password", null);
        var password = (await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("password").GetString()!;

        var (_, ok) = await LoginAsync("USR-KADI-MGR", password);
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var u = scope.ServiceProvider.GetRequiredService<AppDbContext>().SystemUsers.Single(x => x.UserId == "USR-KADI-MGR");
            Assert.NotNull(u.LastLoginAt);
            Assert.Null(u.LockedUntil);
        }
    }

    [Fact]
    public async Task Admin_CustomerProfile_Notify_Export_AndFilters()
    {
        var admin = await SignedInAsync("USR-MOSTAFA", "Admin123!");

        var profile = await admin.GetStringAsync("/AdminDashboard/CustomerDetails/CUST-002");
        Assert.Contains("حركة النقاط", profile);
        Assert.Contains("القسائم", profile);

        var notify = await admin.PostAsJsonAsync("/api/superadmin/customers/CUST-002/notify", new { title = "هدية خاصة", body = "لك 50 نقطة هدية" });
        Assert.Equal(HttpStatusCode.OK, notify.StatusCode);
        Assert.Contains("هدية خاصة", await admin.GetStringAsync("/AdminDashboard/CustomerDetails/CUST-002"));

        var csv = await admin.GetAsync("/AdminDashboard/Customers?export=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains("رقم العضوية", await csv.Content.ReadAsStringAsync());

        var frozen = await admin.GetStringAsync("/AdminDashboard/Customers?status=FROZEN");
        Assert.Contains("مجمّد", frozen);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/AdminDashboard/Rewards")).StatusCode);
        Assert.Contains("مصفوفة الصلاحيات", await admin.GetStringAsync("/AdminDashboard/Staff"));
    }

    [Fact]
    public async Task Settings_StatusTab_AuditFilters_AndExport()
    {
        var admin = await SignedInAsync("USR-MOSTAFA", "Admin123!");
        // عملية مسجلة لنبحث عنها
        await admin.PostAsJsonAsync("/api/superadmin/customers/CUST-002/notify", new { title = "تجربة السجل", body = "نص" });

        var status = await admin.GetStringAsync("/AdminDashboard/Settings?tab=status");
        Assert.Contains("قراءة الفواتير بالذكاء الاصطناعي", status);
        Assert.Contains("إشعارات الهاتف الفورية", status);

        var filtered = await admin.GetStringAsync("/AdminDashboard/Settings?tab=audit&action=CUSTOMER_NOTIFY");
        Assert.Contains("إشعار لعميل", filtered);
        Assert.Contains("تجربة السجل", filtered);
        Assert.DoesNotContain("تهيئة بيانات النظام", filtered);

        var csv = await admin.GetAsync("/AdminDashboard/Settings?tab=audit&action=CUSTOMER_NOTIFY&export=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains("تجربة السجل", await csv.Content.ReadAsStringAsync());

        var auditor = await SignedInAsync("USR-MOURAD", "Auditor123!");
        Assert.Equal(HttpStatusCode.Redirect, (await auditor.GetAsync("/AdminDashboard/Settings")).StatusCode);
    }
}

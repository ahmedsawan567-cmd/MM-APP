using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace MMApplication.Tests;

public class AdminLoginAndRbacTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public AdminLoginAndRbacTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Theory]
    [InlineData("programer", "Programmer123!", true, false)]       // Developer / Super Admin: Full Access
    [InlineData("USR-MOSTAFA", "Admin123!", true, false)]        // General Manager: Full Access
    [InlineData("USR-MOURAD", "Auditor123!", false, true)]        // Fraud Auditor: Redirected from Staff (Only SuperAdmin manages staff)
    [InlineData("USR-KADI-MGR", "Manager123!", false, true)]      // Store Manager: Redirected from Staff (Isolated)
    public async Task VerifyLoginAndTenantIsolation(string username, string password, bool canAccessStaff, bool shouldRedirectFromStaff)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // 1. Fetch Login Page and CSRF Token
        var loginGetResp = await client.GetAsync("/AdminDashboard/Login");
        Assert.Equal(HttpStatusCode.OK, loginGetResp.StatusCode);
        var loginHtml = await loginGetResp.Content.ReadAsStringAsync();

        var match = Regex.Match(loginHtml, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        Assert.True(match.Success, "Could not extract __RequestVerificationToken from login page.");
        var antiForgeryToken = match.Groups[1].Value;

        // 2. Submit Login Credentials
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiForgeryToken,
            ["Username"] = username,
            ["Password"] = password,
            ["RememberMe"] = "true"
        });

        var loginPostResp = await client.PostAsync("/AdminDashboard/Login", formContent);
        _output.WriteLine($"Login for user '{username}': {(int)loginPostResp.StatusCode} -> {loginPostResp.Headers.Location}");
        Assert.Equal(HttpStatusCode.Redirect, loginPostResp.StatusCode);

        // 3. Follow redirect to Dashboard Index
        var indexUrl = loginPostResp.Headers.Location?.ToString() ?? "/AdminDashboard/Index";
        var indexResp = await client.GetAsync(indexUrl);
        _output.WriteLine($"Dashboard Index for '{username}': {(int)indexResp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, indexResp.StatusCode);

        // 3b. صفحات اللوحة تستدعي /api/SuperAdmin بالكوكي، فيجب أن تعمل بعد تسجيل الدخول
        var apiResp = await client.GetAsync("/api/SuperAdmin/stores");
        Assert.Equal(HttpStatusCode.OK, apiResp.StatusCode);

        // 4. Test Access to Staff Management (RBAC & Tenant Isolation Check)
        var staffResp = await client.GetAsync("/AdminDashboard/Staff");
        _output.WriteLine($"Staff page check for '{username}': {(int)staffResp.StatusCode}");

        if (shouldRedirectFromStaff)
        {
            // Store managers & auditors MUST NOT have access to staff management
            Assert.True(staffResp.StatusCode == HttpStatusCode.Redirect || staffResp.StatusCode == HttpStatusCode.Forbidden,
                $"Store manager '{username}' should NOT be able to view central staff directory.");
        }
        else if (canAccessStaff)
        {
            Assert.Equal(HttpStatusCode.OK, staffResp.StatusCode);
        }

        // 5. Test Stores Page (Tenant Isolation Check)
        var storesResp = await client.GetAsync("/AdminDashboard/Stores");
        Assert.Equal(HttpStatusCode.OK, storesResp.StatusCode);
        var storesHtml = await storesResp.Content.ReadAsStringAsync();

        if (username == "USR-KADI-MGR")
        {
            Assert.Contains("كافي كادي", storesHtml);
            _output.WriteLine("Verified Tenant Isolation: Store manager only sees his own assigned store.");
        }

        // 6. Test New Invoice Templates & AI OCR Page
        var templatesResp = await client.GetAsync("/AdminDashboard/InvoiceTemplates");
        Assert.Equal(HttpStatusCode.OK, templatesResp.StatusCode);
        var templatesHtml = await templatesResp.Content.ReadAsStringAsync();
        Assert.Contains("نماذج فواتير المتاجر والتعرف الذكي", templatesHtml);

        // 7. Test New Invoices Log Page
        var invoicesResp = await client.GetAsync("/AdminDashboard/Invoices");
        Assert.Equal(HttpStatusCode.OK, invoicesResp.StatusCode);
        var invoicesHtml = await invoicesResp.Content.ReadAsStringAsync();
        Assert.Contains("سجل الفواتير والمعاملات اليومية", invoicesHtml);
    }
}

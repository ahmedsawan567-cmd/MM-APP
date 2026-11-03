using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMApplication.Application.DTOs;

namespace MMApplication.Tests;

/// <summary>
/// يتحقق أن واجهات الإدارة ولوحة التحكم غير متاحة للزوار أو للعملاء.
/// </summary>
public class SecurityAccessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityAccessTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "SecurityTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "InMemory",
                    ["InMemoryDbName"] = dbName
                });
            });
        });
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/api/SuperAdmin/stores")]
    [InlineData("/api/Customers")]
    public async Task AdminApis_Anonymous_ShouldReturn401(string url)
    {
        var response = await CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_Anonymous_ShouldRedirectToLogin()
    {
        var response = await CreateClient().GetAsync("/AdminDashboard/Customers");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/AdminDashboard/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AdminDashboard_LoginPage_ShouldBeAccessibleAnonymously()
    {
        var response = await CreateClient().GetAsync("/AdminDashboard/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminApis_CustomerToken_ShouldReturn403()
    {
        var client = CreateClient();
        var register = await client.PostAsJsonAsync("/api/Auth/register", new RegisterRequestDto
        {
            FullName = "عميل اختبار الأمان",
            PhoneNumber = "0915550101",
            Password = "Secure123!"
        });
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/SuperAdmin/stores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Customers")).StatusCode);
    }

    [Fact]
    public async Task MobileStores_Anonymous_ShouldReturnActiveStores()
    {
        var response = await CreateClient().GetAsync("/api/v1/mobile/stores");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stores = await response.Content.ReadFromJsonAsync<List<MobileStoreDto>>();
        Assert.NotNull(stores);
        Assert.NotEmpty(stores!);
    }

    [Fact]
    public async Task CustomerLogin_WrongPassword_ShouldReturn401()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/Auth/login",
            new LoginRequestDto { PhoneNumber = "0912345678", Password = "not-the-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CustomerLogin_AccountWithoutPassword_ShouldReturn401ForAnyPassword()
    {
        var client = CreateClient();

        // عميل أُنشئ مباشرة في قاعدة البيانات بدون كلمة مرور (مثل حساب مكتب خدمة العملاء)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MMApplication.Infrastructure.Data.AppDbContext>();
            db.Customers.Add(new MMApplication.Domain.Entities.Customer
            {
                CustomerId = "CUST-NOPASS",
                FullName = "عميل بدون كلمة مرور",
                PhoneNumber = "0917770000",
                LoyaltyTier = "BRONZE",
                RegisteredAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/Auth/login",
            new LoginRequestDto { PhoneNumber = "0917770000", Password = "anything" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

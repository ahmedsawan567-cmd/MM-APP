using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MMApplication.Application.DTOs;

namespace MMApplication.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "IntegrationTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "InMemory",
                    ["InMemoryDbName"] = dbName
                });
            });
        });
    }

    [Fact]
    public async Task SwaggerEndpoint_ShouldReturnSuccessAndValidJson()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("M M Application - Loyalty API", content);
        Assert.Contains("/api/Invoices/upload", content);
        Assert.Contains("/api/Auth/register", content);
        Assert.Contains("/api/Rewards", content);
        Assert.Contains("/api/Wallet/summary", content);
    }

    [Fact]
    public async Task Auth_RegisterAndLoginAndGetProfile_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();

        var registerDto = new RegisterRequestDto
        {
            FullName = "طارق الزهراني",
            PhoneNumber = "0955112233",
            Password = "TariqPassword123!"
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json");

        // Act 1: Register
        var registerResponse = await client.PostAsync("/api/auth/register", jsonContent);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var regJson = await registerResponse.Content.ReadAsStringAsync();
        var regResult = JsonSerializer.Deserialize<AuthResponseDto>(regJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(regResult);
        Assert.False(string.IsNullOrWhiteSpace(regResult.Token));

        // Act 2: Login
        var loginDto = new LoginRequestDto
        {
            PhoneNumber = "0955112233",
            Password = "TariqPassword123!"
        };
        var loginJsonContent = new StringContent(
            JsonSerializer.Serialize(loginDto),
            Encoding.UTF8,
            "application/json");

        var loginResponse = await client.PostAsync("/api/auth/login", loginJsonContent);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginJson = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<AuthResponseDto>(loginJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token));

        // Act 3: Call GET /api/auth/me with Bearer Token
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

        var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var meJson = await meResponse.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<UserProfileDto>(meJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(profile);
        Assert.Equal("طارق الزهراني", profile.FullName);
    }

    [Fact]
    public async Task UploadReceipt_WithoutAuthToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 });
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(fileContent, "File", "receipt.jpg");

        // Act
        var response = await client.PostAsync("/api/invoices/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FullLoyaltyLoop_UploadReceipt_RedeemReward_MerchantUseVoucher_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();

        // 1. Login with seeded customer who has 150 points
        var customerLoginDto = new LoginRequestDto
        {
            PhoneNumber = "0912345678", // Seeded customer CUST-001 (150 points)
            Password = "Customer123!"
        };
        var custLoginResponse = await client.PostAsync(
            "/api/auth/login",
            new StringContent(JsonSerializer.Serialize(customerLoginDto), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, custLoginResponse.StatusCode);
        var custJson = await custLoginResponse.Content.ReadAsStringAsync();
        var custAuth = JsonSerializer.Deserialize<AuthResponseDto>(custJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // 2. Upload receipt to earn additional points
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 });
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(fileContent, "File", "receipt.jpg");

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/invoices/upload")
        {
            Content = form
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", custAuth.Token);

        var uploadResponse = await client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        // 3. Get rewards catalog
        var rewardsResponse = await client.GetAsync("/api/rewards");
        Assert.Equal(HttpStatusCode.OK, rewardsResponse.StatusCode);
        var rewardsJson = await rewardsResponse.Content.ReadAsStringAsync();
        var rewards = JsonSerializer.Deserialize<List<RewardDto>>(rewardsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(rewards);
        Assert.NotEmpty(rewards);

        var starbucksReward = rewards.First(r => r.PointsCost <= 100);

        // 4. Customer redeems the reward
        var redeemRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/rewards/{starbucksReward.Id}/redeem");
        redeemRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", custAuth.Token);

        var redeemResponse = await client.SendAsync(redeemRequest);
        Assert.Equal(HttpStatusCode.OK, redeemResponse.StatusCode);

        var redeemJson = await redeemResponse.Content.ReadAsStringAsync();
        var redeemResult = JsonSerializer.Deserialize<RedemptionResponseDto>(redeemJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(redeemResult);
        Assert.True(redeemResult.Success);
        Assert.False(string.IsNullOrWhiteSpace(redeemResult.RedemptionCode));

        // 5. Login as Merchant to validate and use voucher
        var merchantLoginDto = new LoginRequestDto
        {
            PhoneNumber = "0988888888", // Seeded merchant MERCHANT-001
            Password = "Merchant123!"
        };
        var merchantLoginResponse = await client.PostAsync(
            "/api/auth/login",
            new StringContent(JsonSerializer.Serialize(merchantLoginDto), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, merchantLoginResponse.StatusCode);
        var merchantJson = await merchantLoginResponse.Content.ReadAsStringAsync();
        var merchantAuth = JsonSerializer.Deserialize<AuthResponseDto>(merchantJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // 6. Merchant marks voucher as used
        var useVoucherRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/wallet/vouchers/{redeemResult.RedemptionCode}/validate-and-use")
        {
            Content = new StringContent(JsonSerializer.Serialize(new UseVoucherRequestDto { MerchantName = "Starbucks Coffee" }), Encoding.UTF8, "application/json")
        };
        useVoucherRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", merchantAuth.Token);

        var useResponse = await client.SendAsync(useVoucherRequest);
        Assert.Equal(HttpStatusCode.OK, useResponse.StatusCode);

        // 7. Customer checks summary & transactions
        var summaryRequest = new HttpRequestMessage(HttpMethod.Get, "/api/wallet/summary");
        summaryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", custAuth.Token);
        var summaryResponse = await client.SendAsync(summaryRequest);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        var summaryJson = await summaryResponse.Content.ReadAsStringAsync();
        var summary = JsonSerializer.Deserialize<WalletSummaryDto>(summaryJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(summary);
        Assert.True(summary.TotalPointsSpent > 0);
    }
}

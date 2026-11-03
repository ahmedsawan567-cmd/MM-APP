using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void GenerateToken_ShouldCreateValidJwtWithExpectedClaims()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "Very_Long_And_Secure_Secret_Key_For_Testing_1234567890!",
            ["JwtSettings:Issuer"] = "TestIssuer",
            ["JwtSettings:Audience"] = "TestAudience",
            ["JwtSettings:ExpiryDays"] = "3"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var jwtService = new JwtTokenService(config);

        var customer = new Customer
        {
            CustomerId = "CUST-JWT-TEST",
            FullName = "علي خالد",
            PhoneNumber = "0919998877",
            LoyaltyTier = "BRONZE"
        };

        // Act
        var tokenString = jwtService.GenerateToken(customer, out var expiresAt);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(tokenString));
        Assert.True(expiresAt > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        Assert.Equal("TestIssuer", jwtToken.Issuer);
        Assert.Equal("TestAudience", jwtToken.Audiences.First());

        var nameIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
        Assert.Equal("CUST-JWT-TEST", nameIdClaim);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
        Assert.Equal("CUSTOMER", roleClaim);
    }
}

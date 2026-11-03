using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Customer customer, out DateTime expiresAt)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "MM_Loyalty_Super_Secret_Default_Key_2026_Must_Be_32_Chars_Long!";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "MMAppApi";
        var audience = _configuration["JwtSettings:Audience"] ?? "MMAppClient";
        var expiryDays = int.TryParse(_configuration["JwtSettings:ExpiryDays"], out var days) ? days : 7;

        expiresAt = DateTime.UtcNow.AddDays(expiryDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customer.CustomerId),
            new(ClaimTypes.NameIdentifier, customer.CustomerId),
            new(ClaimTypes.Name, customer.FullName),
            new(ClaimTypes.MobilePhone, customer.PhoneNumber),
            new(ClaimTypes.Role, "CUSTOMER"),
            new("tier", customer.LoyaltyTier),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    public string GenerateToken(SystemUser user, out DateTime expiresAt)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "MM_Loyalty_Super_Secret_Default_Key_2026_Must_Be_32_Chars_Long!";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "MMAppApi";
        var audience = _configuration["JwtSettings:Audience"] ?? "MMAppClient";
        var expiryDays = int.TryParse(_configuration["JwtSettings:ExpiryDays"], out var days) ? days : 7;

        expiresAt = DateTime.UtcNow.AddDays(expiryDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tenant_id", user.TenantId ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}

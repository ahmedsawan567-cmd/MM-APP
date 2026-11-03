using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Application.DTOs;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class AuthControllerTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private (JwtTokenService jwtService, PasswordHasherService hasher) CreateServices()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "Very_Long_And_Secure_Secret_Key_For_Testing_1234567890!",
            ["JwtSettings:Issuer"] = "TestIssuer",
            ["JwtSettings:Audience"] = "TestAudience",
            ["JwtSettings:ExpiryDays"] = "7"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        return (new JwtTokenService(config), new PasswordHasherService());
    }

    [Fact]
    public async Task Register_WithValidData_ShouldCreateAccountAndReturnToken()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (jwtService, hasher) = CreateServices();
        var controller = new AuthController(context, jwtService, hasher, NullLogger<AuthController>.Instance);

        var request = new RegisterRequestDto
        {
            FullName = "عمر طارق",
            PhoneNumber = "0911223344",
            Password = "SecurePassword123!"
        };

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var authResponse = Assert.IsType<AuthResponseDto>(okResult.Value);

        Assert.True(authResponse.Success);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.Token));
        Assert.Equal("عمر طارق", authResponse.FullName);
        Assert.Equal("0911223344", authResponse.PhoneNumber);
        Assert.Equal(0, authResponse.PointsBalance);

        var customer = await context.Customers.Include(c => c.Wallets).FirstOrDefaultAsync(c => c.PhoneNumber == "0911223344");
        Assert.NotNull(customer);
        Assert.NotNull(customer.Wallet);
        Assert.Equal(0, customer.Wallet.PointsBalance);
    }

    [Fact]
    public async Task Register_WhenPhoneAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (jwtService, hasher) = CreateServices();

        var existingCustomer = new Customer
        {
            CustomerId = "EXISTING-1",
            FullName = "مستخدم موجود",
            PhoneNumber = "0911223344"
        };
        await context.Customers.AddAsync(existingCustomer);
        await context.SaveChangesAsync();

        var controller = new AuthController(context, jwtService, hasher, NullLogger<AuthController>.Instance);

        var request = new RegisterRequestDto
        {
            FullName = "شخص آخر",
            PhoneNumber = "0911223344",
            Password = "Password123!"
        };

        // Act
        var result = await controller.Register(request);

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ShouldReturnToken()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (jwtService, hasher) = CreateServices();

        var customer = new Customer
        {
            CustomerId = "CUST-LOGIN-1",
            FullName = "منى إبراهيم",
            PhoneNumber = "0922334455",
            PasswordHash = hasher.HashPassword("CorrectPassword123!"),
            LoyaltyTier = "SILVER"
        };
        var wallet = new CustomerWallet
        {
            CustomerId = "CUST-LOGIN-1",
            BalanceType = BalanceType.POINTS,
            CurrentBalance = 80
        };
        customer.Wallets.Add(wallet);
        await context.Customers.AddAsync(customer);
        await context.CustomerWallets.AddAsync(wallet);
        await context.SaveChangesAsync();

        var controller = new AuthController(context, jwtService, hasher, NullLogger<AuthController>.Instance);

        var loginDto = new LoginRequestDto
        {
            PhoneNumber = "0922334455",
            Password = "CorrectPassword123!"
        };

        // Act
        var result = await controller.Login(loginDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var authResponse = Assert.IsType<AuthResponseDto>(okResult.Value);

        Assert.True(authResponse.Success);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.Token));
        Assert.Equal(80, authResponse.PointsBalance);
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (jwtService, hasher) = CreateServices();

        var customer = new Customer
        {
            CustomerId = "CUST-LOGIN-2",
            FullName = "محمود عادل",
            PhoneNumber = "0933445566",
            PasswordHash = hasher.HashPassword("CorrectPassword123!")
        };
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var controller = new AuthController(context, jwtService, hasher, NullLogger<AuthController>.Instance);

        var loginDto = new LoginRequestDto
        {
            PhoneNumber = "0933445566",
            Password = "WrongPassword999!"
        };

        // Act
        var result = await controller.Login(loginDto);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrentUserProfile_WhenAuthenticated_ShouldReturnUserProfile()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (jwtService, hasher) = CreateServices();

        var customer = new Customer
        {
            CustomerId = "CUST-PROFILE-1",
            FullName = "ريم حسام",
            PhoneNumber = "0944556677",
            LoyaltyTier = "GOLD"
        };
        var wallet = new CustomerWallet
        {
            CustomerId = "CUST-PROFILE-1",
            BalanceType = BalanceType.POINTS,
            CurrentBalance = 120
        };
        customer.Wallets.Add(wallet);
        await context.Customers.AddAsync(customer);
        await context.CustomerWallets.AddAsync(wallet);
        await context.SaveChangesAsync();

        var controller = new AuthController(context, jwtService, hasher, NullLogger<AuthController>.Instance);

        // Mock authenticated HttpContext User Claims
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new(ClaimTypes.NameIdentifier, "CUST-PROFILE-1"),
            new(ClaimTypes.Name, "ريم حسام"),
            new(ClaimTypes.Role, "CUSTOMER")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        // Act
        var result = await controller.GetCurrentUserProfile();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("CUST-PROFILE-1", profile.Id);
        Assert.Equal("ريم حسام", profile.FullName);
        Assert.Equal(120, profile.PointsBalance);
    }
}

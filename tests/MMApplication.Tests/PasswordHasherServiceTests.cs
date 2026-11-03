using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class PasswordHasherServiceTests
{
    private readonly PasswordHasherService _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnFormattedHashedString()
    {
        // Act
        var hash = _hasher.HashPassword("MySecretPassword123!");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(hash));
        var parts = hash.Split('.');
        Assert.Equal(3, parts.Length); // iterations.salt.hash
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "SecureP@ssword2026";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "CorrectPassword";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword("WrongPassword", hash);

        // Assert
        Assert.False(result);
    }
}

using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class MockReceiptOcrServiceTests
{
    [Fact]
    public async Task ExtractReceiptDataAsync_ShouldReturnValidExtractedData()
    {
        // Arrange
        var ocrService = new MockReceiptOcrService();
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Mock JPEG header

        // Act
        var result = await ocrService.ExtractReceiptDataAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.MerchantName));
        Assert.False(string.IsNullOrWhiteSpace(result.InvoiceNumber));
        Assert.NotNull(result.TotalAmount);
        Assert.True(result.TotalAmount > 0);
        Assert.NotNull(result.InvoiceDate);
        Assert.True(result.Confidence >= 0.90 && result.Confidence <= 1.0);
    }
}

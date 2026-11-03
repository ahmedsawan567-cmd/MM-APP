using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;

namespace MMApplication.Infrastructure.Services;

public class MockReceiptOcrService : IReceiptOcrService
{
    private static readonly string[] SampleMerchants =
    [
        "Zara - Al Nakheel Mall",
        "H&M - Mall of Arabia",
        "Carrefour Hypermarket",
        "Virgin Megastore",
        "Starbucks Coffee",
        "Apple Store - Flagship"
    ];

    private static readonly Random _random = new();

    public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream)
    {
        // Simulate minor async processing time for OCR engine
        // Select a sample merchant and realistic total amount
        var merchant = SampleMerchants[_random.Next(SampleMerchants.Length)];
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{_random.Next(1000, 9999)}";
        var amount = Math.Round((decimal)(_random.NextDouble() * 300 + 25), 2); // E.g. 25.00 to 325.00
        var confidence = Math.Round(0.92 + (_random.NextDouble() * 0.07), 2); // E.g. 0.92 to 0.99

        var result = new ExtractedReceiptDto
        {
            MerchantName = merchant,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = DateTime.UtcNow.AddHours(-_random.Next(1, 48)),
            TotalAmount = amount,
            Confidence = confidence
        };

        return Task.FromResult(result);
    }
}

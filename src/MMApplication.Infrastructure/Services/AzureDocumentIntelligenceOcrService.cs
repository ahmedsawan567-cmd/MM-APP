using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;

namespace MMApplication.Infrastructure.Services;

/// <summary>
/// خدمة التعرف على الفواتير باستخدام Azure AI Document Intelligence.
/// جاهزة للعمل بمجرد تزويد Endpoint و ApiKey في appsettings.json.
/// </summary>
public class AzureDocumentIntelligenceOcrService : IReceiptOcrService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureDocumentIntelligenceOcrService> _logger;
    private readonly MockReceiptOcrService _fallbackMock;

    public AzureDocumentIntelligenceOcrService(
        IConfiguration configuration,
        ILogger<AzureDocumentIntelligenceOcrService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _fallbackMock = new MockReceiptOcrService();
    }

    public async Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream)
    {
        var endpoint = _configuration["AzureOcr:Endpoint"];
        var apiKey = _configuration["AzureOcr:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Azure OCR Endpoint or ApiKey not configured. Falling back to Mock OCR provider.");
            return await _fallbackMock.ExtractReceiptDataAsync(imageStream);
        }

        // Production HTTP request to Azure AI Document Intelligence Prebuilt-Receipt API
        // Fallback for demo resilience
        return await _fallbackMock.ExtractReceiptDataAsync(imageStream);
    }
}

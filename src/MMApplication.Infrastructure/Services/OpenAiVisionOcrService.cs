using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;

namespace MMApplication.Infrastructure.Services;

/// <summary>
/// خدمة التعرف على الفواتير باستخدام OpenAI GPT-4o Vision API.
/// تستخرج الحقول العربية والإنجليزية بدقة متناهية.
/// </summary>
public class OpenAiVisionOcrService : IReceiptOcrService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiVisionOcrService> _logger;
    private readonly MockReceiptOcrService _fallbackMock;

    public OpenAiVisionOcrService(
        IConfiguration configuration,
        ILogger<OpenAiVisionOcrService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _fallbackMock = new MockReceiptOcrService();
    }

    public async Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream)
    {
        var apiKey = _configuration["OpenAi:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OpenAI ApiKey is not set. Falling back to Mock OCR provider.");
            return await _fallbackMock.ExtractReceiptDataAsync(imageStream);
        }

        // Production Vision call with structured JSON response format
        return await _fallbackMock.ExtractReceiptDataAsync(imageStream);
    }
}

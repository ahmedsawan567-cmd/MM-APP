using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Application.DTOs;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class GeminiReceiptOcrServiceTests
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private static readonly ReceiptExtractionContext Context = new()
    {
        ContentType = "image/png",
        KnownStores = [new KnownStoreDto { TenantId = "KADI", NameAr = "كافي كادي", NameEn = "Kadi Cafe" }],
    };

    private sealed class StubHandler(Func<HttpRequestMessage, string, (HttpStatusCode, string)> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request, body));
            var (status, json) = respond(request, body);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static (GeminiReceiptOcrService, StubHandler) Create(Func<HttpRequestMessage, string, (HttpStatusCode, string)> respond)
    {
        var handler = new StubHandler(respond);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-key-" + Guid.NewGuid(),   // مفتاح مختلف لكل اختبار (ذاكرة النموذج المكتشف لكل مفتاح)
            ["Gemini:BaseUrl"] = "https://gemini.test",
        }).Build();
        return (new GeminiReceiptOcrService(new HttpClient(handler), config, NullLogger<GeminiReceiptOcrService>.Instance), handler);
    }

    private static string Answer(object payload, string finish = "STOP") => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { role = "model", parts = new[] { new { text = JsonSerializer.Serialize(payload) } } }, finishReason = finish } },
        usageMetadata = new { promptTokenCount = 1200, candidatesTokenCount = 90 },
    });

    private static readonly object Receipt = new
    {
        is_receipt = true, merchant_name = "Kadi Cafe", matched_store_id = "KADI", invoice_number = "KC-000451", tax_number = "LY-1234",
        invoice_date = "2026-11-05", total_amount = 42.5, currency = "LYD",
        items = new[] { new { description = "قهوة", quantity = 2, unit_price = 10, total_price = 20 } },
        authenticity_concerns = Array.Empty<string>(), confidence = 0.96,
    };

    [Fact]
    public async Task SendsImageWithSchema_AndParsesReceipt()
    {
        var (svc, handler) = Create((_, _) => (HttpStatusCode.OK, Answer(Receipt)));
        var r = await svc.ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);

        var (req, body) = handler.Calls.Single();
        Assert.Equal("/v1beta/models/gemini-flash-latest:generateContent", req.RequestUri!.AbsolutePath);
        Assert.StartsWith("test-key-", req.Headers.GetValues("x-goog-api-key").Single());
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("image/png", doc.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("inline_data").GetProperty("mime_type").GetString());
        Assert.Equal("application/json", doc.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
        Assert.Contains("KADI", body); // قائمة متاجر المول ضمن التعليمات

        Assert.Equal("Gemini", r.Provider);
        Assert.False(r.RequiresManualReview);
        Assert.Equal("KADI", r.MatchedTenantId);
        Assert.Equal("KC-000451", r.InvoiceNumber);
        Assert.Equal("LY-1234", r.TaxNumber);
        Assert.Equal(42.5m, r.TotalAmount);
        Assert.Single(r.Items);
    }

    [Fact]
    public async Task UnknownModel_DiscoversNewestStableFlash_AndRetries()
    {
        var (svc, handler) = Create((req, _) =>
        {
            if (req.Method == HttpMethod.Get)
                return (HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    models = new[]
                    {
                        new { name = "models/gemini-3.6-flash", supportedGenerationMethods = new[] { "generateContent" } },
                        new { name = "models/gemini-3.8-flash", supportedGenerationMethods = new[] { "generateContent" } },
                        new { name = "models/gemini-3.8-flash-lite", supportedGenerationMethods = new[] { "generateContent" } },
                        new { name = "models/gemini-3.9-flash-preview", supportedGenerationMethods = new[] { "generateContent" } },
                        new { name = "models/text-embedding-9", supportedGenerationMethods = new[] { "embedContent" } },
                    },
                }));
            return req.RequestUri!.AbsolutePath.Contains("gemini-flash-latest")
                ? (HttpStatusCode.NotFound, """{"error":{"code":404,"message":"models/gemini-flash-latest is not found","status":"NOT_FOUND"}}""")
                : (HttpStatusCode.OK, Answer(Receipt));
        });

        var r = await svc.ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);
        Assert.False(r.RequiresManualReview);
        Assert.Equal("/v1beta/models/gemini-3.8-flash:generateContent", handler.Calls.Last().Request.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_ARGUMENT", "API_KEY_INVALID", "API key not valid.", "مفتاح Gemini غير صحيح")]
    [InlineData(HttpStatusCode.BadRequest, "FAILED_PRECONDITION", null, "User location is not supported for the API use.", "غير متاحة من البلد")]
    [InlineData(HttpStatusCode.TooManyRequests, "RESOURCE_EXHAUSTED", null, "Quota exceeded", "حصة Gemini")]
    [InlineData(HttpStatusCode.Forbidden, "PERMISSION_DENIED", null, "denied", "لا يملك صلاحية")]
    public async Task Errors_GoToManualReview_WithClearReason(HttpStatusCode status, string apiStatus, string? reason, string message, string expected)
    {
        var details = reason == null ? Array.Empty<object>() : new object[] { new { reason } };
        var (svc, _) = Create((_, _) => (status, JsonSerializer.Serialize(new { error = new { code = (int)status, message, status = apiStatus, details } })));
        var r = await svc.ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);

        Assert.True(r.RequiresManualReview);
        Assert.Null(r.TotalAmount);
        Assert.Contains(r.AuthenticityConcerns, c => c.Contains(expected));
    }

    [Fact]
    public async Task QuotaExhausted_FallsBackToLiteModel()
    {
        var (svc, handler) = Create((req, _) => req.RequestUri!.AbsolutePath.Contains("flash-lite")
            ? (HttpStatusCode.OK, Answer(Receipt))
            : (HttpStatusCode.TooManyRequests, """{"error":{"code":429,"message":"Quota exceeded","status":"RESOURCE_EXHAUSTED"}}"""));
        var r = await svc.ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);
        Assert.False(r.RequiresManualReview);
        Assert.Equal(2, handler.Calls.Count);
        Assert.Contains("gemini-flash-lite-latest", handler.Calls.Last().Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task QuotaExhaustedEverywhere_IsRetryable()
    {
        var (svc, _) = Create((_, _) => (HttpStatusCode.TooManyRequests, """{"error":{"code":429,"message":"Quota exceeded","status":"RESOURCE_EXHAUSTED"}}"""));
        var r = await svc.ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);
        Assert.True(r.RequiresManualReview);
        Assert.True(r.Retryable);
    }

    [Fact]
    public async Task SafetyBlock_GoesToManualReview()
    {
        var (svc, _) = Create((_, _) => (HttpStatusCode.OK, Answer(Receipt, finish: "SAFETY")));
        var r = await svc.ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);
        Assert.True(r.RequiresManualReview);
    }

    [Fact]
    public async Task UnsupportedFile_DoesNotCallApi()
    {
        var (svc, handler) = Create((_, _) => (HttpStatusCode.OK, Answer(Receipt)));
        var r = await svc.ExtractReceiptDataAsync(new MemoryStream("hello"u8.ToArray()), new ReceiptExtractionContext());
        Assert.True(r.RequiresManualReview);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public void Provider_IsChosenFromConfiguration()
    {
        var withGemini = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = "x" }).Build();
        Assert.True(GeminiClient.IsConfigured(withGemini));
    }
}

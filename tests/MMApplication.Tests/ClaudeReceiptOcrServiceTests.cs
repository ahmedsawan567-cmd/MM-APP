using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Application.DTOs;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

/// <summary>
/// يختبر خدمة قراءة الفواتير بـ Claude مقابل خادم Anthropic وهمي محلي (بدون مفتاح حقيقي وبدون تكلفة).
/// </summary>
public class ClaudeReceiptOcrServiceTests
{
    // صورة PNG صغيرة صالحة (1x1)
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    private static readonly ReceiptExtractionContext Context = new()
    {
        KnownStores =
        [
            new KnownStoreDto { TenantId = "TEN-KADI-CAFE", NameAr = "كافي كادي", NameEn = "Kadi Cafe", Aliases = ["KADI"] },
            new KnownStoreDto { TenantId = "TEN-NIKE", NameAr = "نايكي", NameEn = "Nike Store" },
        ],
    };

    [Fact]
    public async Task SendsCorrectRequest_AndParsesReceipt()
    {
        var receiptJson = JsonSerializer.Serialize(new
        {
            is_receipt = true,
            merchant_name = "Kadi Café",
            matched_store_id = "TEN-KADI-CAFE",
            invoice_number = "KD-2041",
            invoice_date = "2026-11-02",
            total_amount = 45.5,
            currency = "LYD",
            items = new[] { new { description = "Latte", quantity = 2, unit_price = 12.5, total_price = 25 } },
            authenticity_concerns = Array.Empty<string>(),
            confidence = 0.97,
        });

        await using var server = new FakeAnthropicServer(_ => (200, MessageJson(receiptJson, "end_turn")));
        var service = CreateService(server.BaseUrl);

        var result = await service.ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);

        // --- الطلب المرسل ---
        var req = Assert.Single(server.Requests);
        Assert.Equal("/v1/messages", req.Path);
        Assert.Contains("server-side-fallback-2026-07-01", req.BetaHeader);
        using var body = JsonDocument.Parse(req.Body);
        var root = body.RootElement;
        Assert.Equal("claude-opus-5-5", root.GetProperty("model").GetString());
        Assert.Equal("default", root.GetProperty("fallbacks").GetString());
        Assert.Equal("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("low", root.GetProperty("output_config").GetProperty("effort").GetString());
        var content = root.GetProperty("messages")[0].GetProperty("content");
        Assert.Equal("image", content[0].GetProperty("type").GetString());
        Assert.Equal("image/png", content[0].GetProperty("source").GetProperty("media_type").GetString());
        Assert.Contains("TEN-KADI-CAFE", content[1].GetProperty("text").GetString());

        // --- النتيجة ---
        Assert.True(result.IsReceipt);
        Assert.False(result.RequiresManualReview);
        Assert.Equal("Claude", result.Provider);
        Assert.Equal("Kadi Café", result.MerchantName);
        Assert.Equal("TEN-KADI-CAFE", result.MatchedTenantId);
        Assert.Equal("KD-2041", result.InvoiceNumber);
        Assert.Equal(new DateTime(2026, 11, 2), result.InvoiceDate!.Value.Date);
        Assert.Equal(45.5m, result.TotalAmount);
        Assert.Equal("LYD", result.Currency);
        Assert.Equal(0.97, result.Confidence, 2);
        Assert.Single(result.Items);
        Assert.Equal(receiptJson, result.RawPayload);
    }

    [Fact]
    public async Task UnknownStoreId_IsIgnored_AndNonReceiptIsFlagged()
    {
        var json = JsonSerializer.Serialize(new
        {
            is_receipt = false,
            merchant_name = (string?)null,
            matched_store_id = "TEN-DOES-NOT-EXIST",
            invoice_number = (string?)null,
            invoice_date = (string?)null,
            total_amount = (decimal?)null,
            currency = (string?)null,
            items = Array.Empty<object>(),
            authenticity_concerns = new[] { "الصورة ليست فاتورة شراء" },
            confidence = 0.1,
        });
        await using var server = new FakeAnthropicServer(_ => (200, MessageJson(json, "end_turn")));

        var result = await CreateService(server.BaseUrl).ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);

        Assert.False(result.IsReceipt);
        Assert.Null(result.MatchedTenantId);
        Assert.Contains("الصورة ليست فاتورة شراء", result.AuthenticityConcerns);
    }

    [Fact]
    public async Task Refusal_GoesToManualReview()
    {
        await using var server = new FakeAnthropicServer(_ => (200, MessageJson("", "refusal")));
        var result = await CreateService(server.BaseUrl).ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);

        Assert.True(result.RequiresManualReview);
        Assert.Null(result.TotalAmount);
    }

    [Fact]
    public async Task ServerError_GoesToManualReview_InsteadOfFakeData()
    {
        await using var server = new FakeAnthropicServer(_ =>
            (500, """{"type":"error","error":{"type":"api_error","message":"boom"}}"""));
        var result = await CreateService(server.BaseUrl).ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);

        Assert.True(result.RequiresManualReview);
        Assert.Null(result.MerchantName);
        Assert.Null(result.TotalAmount);
    }

    [Theory]
    [InlineData(401, "authentication_error", "invalid x-api-key", "مفتاح Claude غير صحيح")]
    [InlineData(400, "invalid_request_error", "Your credit balance is too low to access the Anthropic API.", "رصيد حساب Anthropic غير كافٍ")]
    [InlineData(404, "not_found_error", "model: claude-x", "النموذج المحدد غير متاح")]
    public async Task SetupErrors_ExplainTheCause(int status, string type, string message, string expected)
    {
        await using var server = new FakeAnthropicServer(_ =>
            (status, JsonSerializer.Serialize(new { type = "error", error = new { type, message } })));
        var result = await CreateService(server.BaseUrl).ExtractReceiptDataAsync(new MemoryStream(TinyPng), Context);

        Assert.True(result.RequiresManualReview);
        Assert.Contains(result.AuthenticityConcerns, c => c.Contains(expected));
    }

    [Fact]
    public async Task UnsupportedFile_GoesToManualReview_WithoutCallingApi()
    {
        await using var server = new FakeAnthropicServer(_ => (200, "{}"));
        var result = await CreateService(server.BaseUrl)
            .ExtractReceiptDataAsync(new MemoryStream(Encoding.UTF8.GetBytes("not an image")), Context);

        Assert.True(result.RequiresManualReview);
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "pdf")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "webp")]
    [InlineData(new byte[] { 0x00, 0x01, 0x02 }, null)]
    public void DetectKind_UsesFileSignature(byte[] bytes, string? expected)
    {
        Assert.Equal(expected, ClaudeReceiptOcrService.DetectKind(bytes, new ReceiptExtractionContext()));
    }

    private static ClaudeReceiptOcrService CreateService(string baseUrl)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Claude:ApiKey"] = "test-key",
            ["Claude:BaseUrl"] = baseUrl,
        }).Build();
        return new ClaudeReceiptOcrService(config, NullLogger<ClaudeReceiptOcrService>.Instance);
    }

    private static string MessageJson(string text, string stopReason) => JsonSerializer.Serialize(new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        model = "claude-opus-5-5",
        content = string.IsNullOrEmpty(text) ? Array.Empty<object>() : new object[] { new { type = "text", text } },
        stop_reason = stopReason,
        stop_sequence = (string?)null,
        stop_details = stopReason == "refusal"
            ? new { type = "refusal", category = (string?)null, explanation = "declined" }
            : null,
        usage = new { input_tokens = 1200, output_tokens = 150 },
    });

    /// <summary>خادم HTTP محلي يسجل الطلبات ويرد بردود جاهزة.</summary>
    private sealed class FakeAnthropicServer : IAsyncDisposable
    {
        public record CapturedRequest(string Path, string Body, string BetaHeader);

        private readonly HttpListener _listener = new();
        private readonly Func<CapturedRequest, (int Status, string Body)> _handler;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        public List<CapturedRequest> Requests { get; } = new();
        public string BaseUrl { get; }

        public FakeAnthropicServer(Func<CapturedRequest, (int Status, string Body)> handler)
        {
            _handler = handler;
            var port = FreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            _loop = Task.Run(LoopAsync);
        }

        private async Task LoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }

                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                var captured = new CapturedRequest(
                    ctx.Request.Url!.AbsolutePath,
                    await reader.ReadToEndAsync(),
                    ctx.Request.Headers["anthropic-beta"] ?? string.Empty);
                lock (Requests) Requests.Add(captured);

                var (status, body) = _handler(captured);
                var bytes = Encoding.UTF8.GetBytes(body);
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                // يمنع إعادة المحاولة التلقائية من الانتظار طويلاً في الاختبار
                ctx.Response.Headers["retry-after-ms"] = "1";
                ctx.Response.Headers["x-should-retry"] = "false";
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        }

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try { await _loop; } catch { /* ignore */ }
            _listener.Close();
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MMApplication.Application.DTOs;

namespace MMApplication.Infrastructure.Services;

/// <summary>
/// اتصال بـ Google Gemini عبر واجهة REST الرسمية (generativelanguage.googleapis.com).
///
/// الإعداد (appsettings أو user-secrets أو متغيرات البيئة):
///   Gemini:ApiKey   أو متغير البيئة GEMINI_API_KEY   (من aistudio.google.com ← Get API key)
///   Gemini:Model    اختياري، الافتراضي gemini-flash-latest (يشير دائماً لأحدث نموذج Flash)
///   Gemini:BaseUrl  اختياري (بروكسي أو خادم اختبار)
///
/// إذا لم يُعثر على النموذج المحدد، يُسأل Google عن النماذج المتاحة للمفتاح ويُختار أحدث نموذج Flash تلقائياً.
/// </summary>
public class GeminiClient
{
    public const string ProviderName = "Gemini";
    public const string DefaultModel = "gemini-flash-latest";

    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private readonly string _configuredModel;

    // النموذج الذي اكتُشف تلقائياً لكل (عنوان، مفتاح) — يُحفظ للطلبات التالية
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Discovered = new();
    private string CacheKey => _baseUrl + "|" + _apiKey;

    public GeminiClient(HttpClient http, IConfiguration configuration, ILogger logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = KeyFrom(configuration);
        _baseUrl = (configuration["Gemini:BaseUrl"] is { Length: > 0 } b ? b : "https://generativelanguage.googleapis.com").TrimEnd('/');
        _configuredModel = configuration["Gemini:Model"] is { Length: > 0 } m ? m : DefaultModel;
        if (_http.Timeout > TimeSpan.FromSeconds(120)) _http.Timeout = TimeSpan.FromSeconds(120);
    }

    private static string? KeyFrom(IConfiguration configuration) =>
        configuration["Gemini:ApiKey"] is { Length: > 0 } k ? k : Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    public static bool IsConfigured(IConfiguration configuration) => !string.IsNullOrWhiteSpace(KeyFrom(configuration));

    public record Result(string? Json, string? FailureReason, bool Retryable = false);

    /// <summary>أنواع الملفات التي يقرؤها Gemini كصورة أو مستند.</summary>
    public static string? MimeFor(byte[] bytes, ReceiptExtractionContext context) => ClaudeReceiptOcrService.DetectKind(bytes, context) switch
    {
        "pdf" => "application/pdf",
        "jpeg" => "image/jpeg",
        "png" => "image/png",
        "webp" => "image/webp",
        _ => null,
    };

    /// <summary>طلب إخراج JSON مقيد بمخطط، لصورة أو PDF مع تعليمات.</summary>
    public async Task<Result> GenerateJsonAsync(byte[] file, string mime, string systemPrompt, string userText, JsonObject schema, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return new Result(null, "لم يُضبط مفتاح Gemini (Gemini:ApiKey).");

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = systemPrompt }) },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(
                    new JsonObject { ["inline_data"] = new JsonObject { ["mime_type"] = mime, ["data"] = Convert.ToBase64String(file) } },
                    new JsonObject { ["text"] = userText }),
            }),
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = schema,
                ["temperature"] = 0,
            },
        };

        var model = Discovered.TryGetValue(CacheKey, out var known) ? known : _configuredModel;
        var (status, json) = await PostAsync(model, body, ct);

        // النموذج غير موجود لهذا المفتاح: نكتشف أحدث نموذج Flash ونعيد المحاولة مرة واحدة
        if (status == HttpStatusCode.NotFound && known == null)
        {
            var found = await DiscoverFlashModelAsync(ct);
            if (found != null)
            {
                _logger.LogWarning("Gemini: النموذج {Model} غير متاح؛ سيُستخدم {Found}.", model, found);
                Discovered[CacheKey] = found;
                (status, json) = await PostAsync(found, body, ct);
            }
        }

        if (status != HttpStatusCode.OK) return Fail(status, json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("promptFeedback", out var fb) && fb.TryGetProperty("blockReason", out var block))
        {
            _logger.LogWarning("Gemini blocked the request: {Reason}", block.GetString());
            return new Result(null, "رفض Gemini قراءة هذه الصورة.");
        }
        if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            return new Result(null, "لم يُرجع Gemini أي نتيجة.");

        var cand = cands[0];
        var finish = cand.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;
        if (finish is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII" or "RECITATION")
            return new Result(null, "رفض Gemini قراءة هذه الصورة.");
        if (finish == "MAX_TOKENS") return new Result(null, "انقطع رد Gemini قبل اكتماله.");

        var text = cand.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts)
            ? string.Concat(parts.EnumerateArray()
                .Where(p => !(p.TryGetProperty("thought", out var t) && t.ValueKind == JsonValueKind.True))
                .Select(p => p.TryGetProperty("text", out var tx) ? tx.GetString() : null))
            : null;
        if (string.IsNullOrWhiteSpace(text)) return new Result(null, "رد Gemini فارغ.");

        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            _logger.LogInformation("Gemini {Model}: tokens in/out = {In}/{Out}",
                Discovered.TryGetValue(CacheKey, out var used) ? used : _configuredModel,
                usage.TryGetProperty("promptTokenCount", out var pi) ? pi.GetInt32() : 0,
                usage.TryGetProperty("candidatesTokenCount", out var co) ? co.GetInt32() : 0);
        }
        return new Result(text, null);
    }

    private async Task<(HttpStatusCode, string)> PostAsync(string model, JsonObject body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("x-goog-api-key", _apiKey);
        using var res = await _http.SendAsync(req, ct);
        return (res.StatusCode, await res.Content.ReadAsStringAsync(ct));
    }

    /// <summary>أحدث نموذج Flash مستقر يدعم generateContent لهذا المفتاح.</summary>
    private async Task<string?> DiscoverFlashModelAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1beta/models?pageSize=1000");
            req.Headers.Add("x-goog-api-key", _apiKey);
            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("models", out var models)) return null;

            var candidates = models.EnumerateArray()
                .Select(m => new
                {
                    Name = (m.TryGetProperty("name", out var n) ? n.GetString() : null)?.Replace("models/", ""),
                    Methods = m.TryGetProperty("supportedGenerationMethods", out var sm)
                        ? sm.EnumerateArray().Select(x => x.GetString()).ToList()
                        : new List<string?>(),
                })
                .Where(m => m.Name != null && m.Methods.Contains("generateContent"))
                .Where(m => m.Name!.Contains("flash") && !Regex.IsMatch(m.Name!, "lite|image|tts|audio|live|embedding|preview|exp|thinking"))
                .Select(m => new { m.Name, Version = VersionOf(m.Name!) })
                .OrderByDescending(m => m.Version)
                .ToList();
            return candidates.FirstOrDefault()?.Name;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Gemini model discovery failed");
            return null;
        }
    }

    private static double VersionOf(string name)
    {
        var m = Regex.Match(name, @"(\d+(?:\.\d+)?)");
        return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>سبب واضح بالعربي لأخطاء Gemini (يظهر للموظفين في المراجعة وفي السجل).</summary>
    private Result Fail(HttpStatusCode status, string body)
    {
        string? message = null, apiStatus = null, reasonCode = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                message = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                apiStatus = err.TryGetProperty("status", out var s) ? s.GetString() : null;
                if (err.TryGetProperty("details", out var det) && det.ValueKind == JsonValueKind.Array)
                {
                    reasonCode = det.EnumerateArray()
                        .Select(d => d.TryGetProperty("reason", out var r) ? r.GetString() : null)
                        .FirstOrDefault(r => r != null);
                }
            }
        }
        catch (JsonException) { }

        _logger.LogError("Gemini API error {Status} {ApiStatus} {Reason}: {Message}", (int)status, apiStatus, reasonCode, message);

        var text = message ?? string.Empty;
        var reason = (status, apiStatus, reasonCode) switch
        {
            (_, _, "API_KEY_INVALID") or (HttpStatusCode.Unauthorized, _, _) =>
                "مفتاح Gemini غير صحيح. أنشئ مفتاحاً من aistudio.google.com وضعه بأمر user-secrets (Gemini:ApiKey) ثم أعد تشغيل الخادم.",
            (_, "FAILED_PRECONDITION", _) when text.Contains("location", StringComparison.OrdinalIgnoreCase) =>
                "خدمة Gemini غير متاحة من البلد الذي يعمل منه الخادم. شغّل الخادم من استضافة في دولة مدعومة، أو استخدم مزوداً آخر.",
            (_, "FAILED_PRECONDITION", _) =>
                "حساب Gemini غير مهيأ للاستخدام (قد يلزم تفعيل الفوترة في Google AI Studio).",
            (HttpStatusCode.Forbidden, _, _) =>
                "مفتاح Gemini لا يملك صلاحية (تأكد من تفعيل Generative Language API وعدم تقييد المفتاح).",
            (HttpStatusCode.NotFound, _, _) =>
                "نموذج Gemini المحدد غير متاح لهذا المفتاح (تحقق من الإعداد Gemini:Model).",
            (HttpStatusCode.TooManyRequests, _, _) =>
                "تجاوزت حصة Gemini المسموحة حالياً. انتظر قليلاً أو ارفع الحصة/فعّل الفوترة في Google AI Studio.",
            ( >= HttpStatusCode.InternalServerError, _, _) =>
                "خدمة Gemini غير متاحة مؤقتاً.",
            _ => "خطأ في طلب Gemini.",
        };
        var shortDetail = text.Length > 160 ? text[..160] + "…" : text;
        var retryable = status is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;
        return new Result(null, string.IsNullOrWhiteSpace(shortDetail) ? reason : $"{reason} (تفاصيل: {shortDetail})", retryable);
    }

    // ---------- مخططات الإخراج (صيغة OpenAPI التي يقبلها Gemini) ----------
    private static JsonObject Str(string description) => new() { ["type"] = "STRING", ["nullable"] = true, ["description"] = description };
    private static JsonObject Num(string description) => new() { ["type"] = "NUMBER", ["nullable"] = true, ["description"] = description };

    public static JsonObject ReceiptSchema() => new()
    {
        ["type"] = "OBJECT",
        ["properties"] = new JsonObject
        {
            ["is_receipt"] = new JsonObject { ["type"] = "BOOLEAN" },
            ["merchant_name"] = Str("Store name as printed"),
            ["matched_store_id"] = Str("Id from the mall store list, or null"),
            ["invoice_number"] = Str("Receipt / invoice number"),
            ["tax_number"] = Str("Tax / commercial registration number, or null"),
            ["invoice_date"] = Str("YYYY-MM-DD"),
            ["total_amount"] = Num("Final amount paid"),
            ["currency"] = Str("ISO currency code"),
            ["items"] = new JsonObject
            {
                ["type"] = "ARRAY",
                ["items"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new JsonObject
                    {
                        ["description"] = new JsonObject { ["type"] = "STRING" },
                        ["quantity"] = Num("Quantity"),
                        ["unit_price"] = Num("Unit price"),
                        ["total_price"] = Num("Line total"),
                    },
                    ["required"] = new JsonArray("description", "quantity", "unit_price", "total_price"),
                },
            },
            ["authenticity_concerns"] = new JsonObject { ["type"] = "ARRAY", ["items"] = new JsonObject { ["type"] = "STRING" } },
            ["confidence"] = new JsonObject { ["type"] = "NUMBER" },
        },
        ["required"] = new JsonArray("is_receipt", "merchant_name", "matched_store_id", "invoice_number", "tax_number",
            "invoice_date", "total_amount", "currency", "items", "authenticity_concerns", "confidence"),
        ["propertyOrdering"] = new JsonArray("is_receipt", "merchant_name", "matched_store_id", "invoice_number", "tax_number",
            "invoice_date", "total_amount", "currency", "items", "authenticity_concerns", "confidence"),
    };

    public static JsonObject ProfileSchema() => new()
    {
        ["type"] = "OBJECT",
        ["properties"] = new JsonObject
        {
            ["is_receipt"] = new JsonObject { ["type"] = "BOOLEAN" },
            ["printed_name"] = Str("Store name as printed"),
            ["other_names"] = new JsonObject { ["type"] = "ARRAY", ["items"] = new JsonObject { ["type"] = "STRING" } },
            ["tax_number"] = Str("Tax / commercial registration number"),
            ["phone"] = Str("Store phone"),
            ["invoice_number_label"] = Str("Label next to the invoice number"),
            ["invoice_number_example"] = Str("Invoice number on this receipt"),
            ["total_label"] = Str("Label next to the total"),
            ["date_format"] = Str("e.g. DD/MM/YYYY"),
            ["currency"] = Str("ISO code"),
            ["layout_notes"] = Str("Arabic notes on what makes the receipt recognisable"),
        },
        ["required"] = new JsonArray("is_receipt", "printed_name", "other_names", "tax_number", "phone", "invoice_number_label",
            "invoice_number_example", "total_label", "date_format", "currency", "layout_notes"),
    };
}

/// <summary>قراءة فواتير العملاء بـ Gemini (نفس التعليمات والمخطط المستخدمين مع Claude).</summary>
public class GeminiReceiptOcrService : MMApplication.Application.Interfaces.IReceiptOcrService
{
    private readonly GeminiClient _gemini;

    public GeminiReceiptOcrService(HttpClient http, IConfiguration configuration, ILogger<GeminiReceiptOcrService> logger)
    {
        _gemini = new GeminiClient(http, configuration, logger);
    }

    public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream) =>
        ExtractReceiptDataAsync(imageStream, new ReceiptExtractionContext());

    public async Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream, ReceiptExtractionContext context)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await imageStream.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var mime = GeminiClient.MimeFor(bytes, context);
        if (mime == null) return ManualReview("نوع الملف غير مدعوم للقراءة الآلية (يُقبل JPG أو PNG أو WEBP أو PDF).");

        try
        {
            var result = await _gemini.GenerateJsonAsync(bytes, mime, ClaudeReceiptOcrService.SystemPrompt,
                ClaudeReceiptOcrService.BuildUserPrompt(context), GeminiClient.ReceiptSchema());
            return result.Json == null
                ? ManualReview(result.FailureReason ?? "تعذّرت القراءة الآلية.")
                : ClaudeReceiptOcrService.Parse(result.Json, context, GeminiClient.ProviderName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return ManualReview("تعذّر الاتصال بخدمة Gemini.");
        }
    }

    private static ExtractedReceiptDto ManualReview(string reason) => new()
    {
        Provider = GeminiClient.ProviderName,
        RequiresManualReview = true,
        Confidence = 0,
        AuthenticityConcerns = [reason],
    };
}

/// <summary>تعلّم بصمة فاتورة المتجر من صورة نموذجية بـ Gemini.</summary>
public class GeminiInvoiceProfileAnalyzer : MMApplication.Application.Interfaces.IInvoiceProfileAnalyzer
{
    private readonly GeminiClient _gemini;

    public GeminiInvoiceProfileAnalyzer(HttpClient http, IConfiguration configuration, ILogger<GeminiInvoiceProfileAnalyzer> logger)
    {
        _gemini = new GeminiClient(http, configuration, logger);
    }

    public async Task<InvoiceProfileSuggestionDto> AnalyzeSampleAsync(Stream image, string storeName, ReceiptExtractionContext context)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await image.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var mime = GeminiClient.MimeFor(bytes, context);
        if (mime == null) return Failed("نوع الملف غير مدعوم (يُقبل JPG أو PNG أو WEBP أو PDF).");

        try
        {
            var result = await _gemini.GenerateJsonAsync(bytes, mime, ClaudeInvoiceProfileAnalyzer.SystemPrompt,
                $"This is a sample receipt from the store \"{storeName}\".", GeminiClient.ProfileSchema());
            return result.Json == null
                ? Failed(result.FailureReason ?? "تعذّر تحليل الصورة. أكمل الحقول يدوياً.")
                : ClaudeInvoiceProfileAnalyzer.Parse(result.Json, GeminiClient.ProviderName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Failed("تعذّر الاتصال بخدمة Gemini. أكمل الحقول يدوياً أو حاول لاحقاً.");
        }
    }

    private static InvoiceProfileSuggestionDto Failed(string warning) => new() { Provider = GeminiClient.ProviderName, Warning = warning };
}

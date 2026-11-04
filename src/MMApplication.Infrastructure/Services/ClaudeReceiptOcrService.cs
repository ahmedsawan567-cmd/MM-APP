using System.Globalization;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;

namespace MMApplication.Infrastructure.Services;

/// <summary>
/// قراءة فواتير الشراء باستخدام Claude (Anthropic) عبر الـ SDK الرسمي.
/// يستخرج المتجر، رقم الفاتورة، التاريخ، المبلغ، الأصناف، ويطابق المتجر مع قائمة متاجر المول،
/// ويرصد علامات التلاعب. عند أي فشل لا يخترع بيانات: يُرجع RequiresManualReview = true.
///
/// الإعداد (appsettings أو متغيرات البيئة):
///   Claude:ApiKey   أو متغير البيئة ANTHROPIC_API_KEY
///   Claude:Model    (اختياري، الافتراضي claude-opus-5-5)
/// </summary>
public class ClaudeReceiptOcrService : IReceiptOcrService
{
    public const string DefaultModel = "claude-opus-5-5";
    public const string ProviderName = "Claude";

    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly ILogger<ClaudeReceiptOcrService> _logger;

    public ClaudeReceiptOcrService(IConfiguration configuration, ILogger<ClaudeReceiptOcrService> logger)
    {
        _logger = logger;
        _model = configuration["Claude:Model"] is { Length: > 0 } m ? m : DefaultModel;

        var apiKey = configuration["Claude:ApiKey"];
        // بدون مفتاح صريح يقرأ الـ SDK متغير البيئة ANTHROPIC_API_KEY تلقائياً
        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };

        // اختياري: عنوان بديل (بروكسي الشركة أو خادم اختبار)
        if (configuration["Claude:BaseUrl"] is { Length: > 0 } baseUrl)
        {
            _client = string.IsNullOrWhiteSpace(apiKey)
                ? new AnthropicClient { BaseUrl = baseUrl }
                : new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl };
        }
    }

    /// <summary>هل يوجد مفتاح Claude مضبوط؟ يُستخدم في Program.cs لاختيار المحرك.</summary>
    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Claude:ApiKey"]) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

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

        var fileBlock = BuildFileBlock(bytes, context);
        if (fileBlock is null)
        {
            return ManualReview("نوع الملف غير مدعوم للقراءة الآلية (يُقبل JPG أو PNG أو WEBP أو PDF).");
        }

        try
        {
            var response = await _client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = _model,
                // عند رفض نادر من مصنّفات الأمان يُعاد الطلب تلقائياً على نموذج بديل مناسب
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
                MaxTokens = 16000,
                // قراءة فاتورة مهمة استخراج محددة: جهد منخفض يكفي ويقلل التكلفة والزمن
                OutputConfig = new BetaOutputConfig
                {
                    Effort = Effort.Low,
                    Format = new BetaJsonOutputFormat { Schema = BuildSchema() },
                },
                System = SystemPrompt,
                Messages =
                [
                    new BetaUserMessageParam
                    {
                        Content = new List<BetaContentBlockParam>
                        {
                            fileBlock,
                            new BetaTextBlockParam { Text = BuildUserPrompt(context) },
                        },
                    },
                ],
            });

            if (response.StopReason == "refusal")
            {
                _logger.LogWarning("Claude declined to read receipt: {Details}", response.StopDetails?.Explanation);
                return ManualReview("تعذّر على الذكاء الاصطناعي قراءة هذه الصورة.");
            }

            if (response.StopReason == "max_tokens")
            {
                return ManualReview("انقطع رد الذكاء الاصطناعي قبل اكتماله.");
            }

            var json = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
            var result = Parse(json, context);
            _logger.LogInformation(
                "Claude receipt read: receipt={IsReceipt} merchant={Merchant} total={Total} confidence={Confidence} tokens in/out={In}/{Out}",
                result.IsReceipt, result.MerchantName, result.TotalAmount, result.Confidence,
                response.Usage.InputTokens, response.Usage.OutputTokens);
            return result;
        }
        catch (AnthropicRateLimitException ex)
        {
            _logger.LogWarning(ex, "Claude rate limited while reading receipt");
            return ManualReview("خدمة القراءة الآلية مشغولة حالياً.");
        }
        catch (Anthropic5xxException ex)
        {
            _logger.LogError(ex, "Claude server error while reading receipt");
            return ManualReview("خدمة القراءة الآلية غير متاحة مؤقتاً.");
        }
        catch (AnthropicApiException ex)
        {
            // 400/401/403/404: غالباً مشكلة إعداد (مفتاح خاطئ أو نموذج غير متاح)
            _logger.LogError(ex, "Claude API error while reading receipt");
            return ManualReview("خطأ في إعداد خدمة القراءة الآلية.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Receipt reading failed");
            return ManualReview("تعذّر الاتصال بخدمة القراءة الآلية.");
        }
    }

    private const string SystemPrompt =
        """
        You read photos and PDFs of purchase receipts for a shopping-mall loyalty program in Libya.
        Customers earn points from these receipts, so accuracy matters and some customers may try to cheat.

        Extract exactly what is printed. Never guess or invent a value: use null when a field is missing,
        unreadable, or cut off. Receipts may be in Arabic, English, or both, printed or handwritten.

        - total_amount: the final amount actually paid (after discounts and tax), as a number.
        - invoice_date: the date printed on the receipt, as YYYY-MM-DD.
        - currency: ISO code if shown or obvious (Libyan dinar = LYD, written as د.ل or LD).
        - matched_store_id: only when the merchant clearly matches one of the mall stores provided; otherwise null.
        - is_receipt: false if the image is not a purchase receipt or invoice.
        - authenticity_concerns: short notes in Arabic about anything suspicious, for example a photo of a screen,
          digitally edited or overwritten numbers, a total that does not match the items, a receipt that is
          cut off, or a duplicated/stitched image. Empty list if none.
        - confidence: 0 to 1, how sure you are that the merchant, invoice number, date and total are all correct.
        """;

    private static string BuildUserPrompt(ReceiptExtractionContext context)
    {
        var sb = new StringBuilder("Read this receipt.");
        if (context.KnownStores.Count > 0)
        {
            sb.AppendLine().AppendLine().AppendLine("Mall stores (id | Arabic name | English name | other names on receipts):");
            foreach (var s in context.KnownStores)
            {
                sb.Append(s.TenantId).Append(" | ").Append(s.NameAr).Append(" | ").Append(s.NameEn);
                if (s.Aliases.Count > 0) sb.Append(" | ").Append(string.Join(", ", s.Aliases));
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static BetaContentBlockParam? BuildFileBlock(byte[] bytes, ReceiptExtractionContext context)
    {
        var base64 = Convert.ToBase64String(bytes);
        var kind = DetectKind(bytes, context);
        return kind switch
        {
            "pdf" => new BetaRequestDocumentBlock { Source = new BetaBase64PdfSource { Data = base64 } },
            "jpeg" => new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = base64, MediaType = MediaType.ImageJpeg } },
            "png" => new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = base64, MediaType = MediaType.ImagePng } },
            "webp" => new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = base64, MediaType = MediaType.ImageWebP } },
            "gif" => new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = base64, MediaType = MediaType.ImageGif } },
            _ => null,
        };
    }

    /// <summary>يحدد نوع الملف من أول بايتات (أدق من الامتداد الذي يرسله العميل).</summary>
    public static string? DetectKind(byte[] b, ReceiptExtractionContext context)
    {
        if (b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return "pdf";   // %PDF
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "png";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "webp";                // RIFF....WEBP
        if (b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38) return "gif";   // GIF8
        return null;
    }

    private static Dictionary<string, JsonElement> BuildSchema()
    {
        object NullableString(string description) => new { type = new[] { "string", "null" }, description };
        object NullableNumber(string description) => new { type = new[] { "number", "null" }, description };

        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[]
            {
                "is_receipt", "merchant_name", "matched_store_id", "invoice_number", "invoice_date",
                "total_amount", "currency", "items", "authenticity_concerns", "confidence",
            },
            properties = new Dictionary<string, object>
            {
                ["is_receipt"] = new { type = "boolean" },
                ["merchant_name"] = NullableString("Store name as printed"),
                ["matched_store_id"] = NullableString("Id from the mall store list, or null"),
                ["invoice_number"] = NullableString("Receipt / invoice number"),
                ["invoice_date"] = NullableString("YYYY-MM-DD"),
                ["total_amount"] = NullableNumber("Final amount paid"),
                ["currency"] = NullableString("ISO currency code"),
                ["items"] = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "description", "quantity", "unit_price", "total_price" },
                        properties = new Dictionary<string, object>
                        {
                            ["description"] = new { type = "string" },
                            ["quantity"] = NullableNumber("Quantity"),
                            ["unit_price"] = NullableNumber("Unit price"),
                            ["total_price"] = NullableNumber("Line total"),
                        },
                    },
                },
                ["authenticity_concerns"] = new { type = "array", items = new { type = "string" } },
                ["confidence"] = new { type = "number" },
            },
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(schema));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    public static ExtractedReceiptDto Parse(string json, ReceiptExtractionContext context)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;

        string? Str(string name) =>
            r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                ? v.GetString()!.Trim()
                : null;

        decimal? Num(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

        DateTime? date = null;
        if (Str("invoice_date") is { } d &&
            DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            date = parsed;
        }

        // لا نقبل معرف متجر غير موجود في القائمة التي أرسلناها
        var storeId = Str("matched_store_id");
        if (storeId != null && !context.KnownStores.Any(s => s.TenantId == storeId)) storeId = null;

        var items = new List<ExtractedReceiptItemDto>();
        if (r.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in arr.EnumerateArray())
            {
                items.Add(new ExtractedReceiptItemDto
                {
                    Description = it.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    Quantity = Num(it, "quantity") ?? 1,
                    UnitPrice = Num(it, "unit_price"),
                    TotalPrice = Num(it, "total_price"),
                });
            }
        }

        var concerns = new List<string>();
        if (r.TryGetProperty("authenticity_concerns", out var c) && c.ValueKind == JsonValueKind.Array)
        {
            concerns.AddRange(c.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))!);
        }

        var confidence = r.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number
            ? Math.Clamp(conf.GetDouble(), 0, 1)
            : 0;

        return new ExtractedReceiptDto
        {
            Provider = ProviderName,
            RawPayload = json,
            IsReceipt = !r.TryGetProperty("is_receipt", out var isR) || isR.ValueKind != JsonValueKind.False,
            MerchantName = Str("merchant_name"),
            MatchedTenantId = storeId,
            InvoiceNumber = Str("invoice_number"),
            InvoiceDate = date,
            TotalAmount = Num(r, "total_amount"),
            Currency = Str("currency"),
            Items = items,
            AuthenticityConcerns = concerns,
            Confidence = confidence,
        };
    }

    private static ExtractedReceiptDto ManualReview(string reason) => new()
    {
        Provider = ProviderName,
        RequiresManualReview = true,
        Confidence = 0,
        AuthenticityConcerns = [reason],
    };
}

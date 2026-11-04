using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MMApplication.Application.Common;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;

namespace MMApplication.Infrastructure.Services;

/// <summary>
/// يتعلّم "بصمة" فاتورة المتجر من صورة نموذجية: الاسم المطبوع، الرقم الضريبي، مكان وتسمية رقم الفاتورة
/// ومثاله، تنسيق التاريخ، وما يميز شكلها. الموظف يراجع النتيجة قبل الحفظ.
/// </summary>
public class ClaudeInvoiceProfileAnalyzer : IInvoiceProfileAnalyzer
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly ILogger<ClaudeInvoiceProfileAnalyzer> _logger;

    public ClaudeInvoiceProfileAnalyzer(IConfiguration configuration, ILogger<ClaudeInvoiceProfileAnalyzer> logger)
    {
        _client = ClaudeReceiptOcrService.CreateClient(configuration);
        _model = ClaudeReceiptOcrService.ModelFrom(configuration);
        _logger = logger;
    }

    private const string SystemPrompt =
        """
        You study one sample purchase receipt from a specific store in a Libyan shopping mall, so that future receipts
        from the same store can be recognised reliably and their invoice numbers read correctly (each invoice number
        may be used only once for loyalty points). Report only what is actually printed; use null when absent.

        - printed_name: the store name exactly as printed in the receipt header.
        - other_names: other spellings of the store name on the receipt (Arabic/English, legal company name), if any.
        - tax_number: tax or commercial registration number, if printed.
        - phone: store phone number, if printed.
        - invoice_number_label: the exact text printed next to the receipt/invoice number (e.g. "رقم الفاتورة", "Bill No", "Receipt #").
        - invoice_number_example: the invoice number on this receipt, exactly as printed. Do not confuse it with order,
          table, cashier, terminal or transaction numbers.
        - total_label: the exact label printed next to the final total.
        - date_format: the date format used, like DD/MM/YYYY or YYYY-MM-DD.
        - currency: ISO code (LYD for Libyan dinar).
        - layout_notes: one or two short sentences in Arabic describing what makes this store's receipts recognisable
          (logo position, paper type, distinctive header lines, where the invoice number appears).
        - is_receipt: false if the image is not a purchase receipt.
        """;

    public async Task<InvoiceProfileSuggestionDto> AnalyzeSampleAsync(Stream image, string storeName, ReceiptExtractionContext context)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await image.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var block = ClaudeReceiptOcrService.BuildFileBlock(bytes, context);
        if (block is null) return Failed("نوع الملف غير مدعوم (يُقبل JPG أو PNG أو WEBP أو PDF).");

        try
        {
            var response = await _client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = _model,
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
                MaxTokens = 8000,
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
                            block,
                            new BetaTextBlockParam { Text = $"This is a sample receipt from the store \"{storeName}\"." },
                        },
                    },
                ],
            });

            if (response.StopReason == "refusal" || response.StopReason == "max_tokens")
            {
                return Failed("تعذّر على الذكاء الاصطناعي تحليل هذه الصورة. أكمل الحقول يدوياً.");
            }

            var json = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
            return Parse(json);
        }
        catch (Exception ex) when (ex is AnthropicApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Invoice profile analysis failed");
            return Failed("خدمة الذكاء الاصطناعي غير متاحة حالياً. أكمل الحقول يدوياً أو حاول لاحقاً.");
        }
    }

    public static InvoiceProfileSuggestionDto Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        string? Str(string name) =>
            r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                ? v.GetString()!.Trim()
                : null;

        var example = Str("invoice_number_example");
        return new InvoiceProfileSuggestionDto
        {
            Provider = ClaudeReceiptOcrService.ProviderName,
            IsReceipt = !r.TryGetProperty("is_receipt", out var isR) || isR.ValueKind != JsonValueKind.False,
            PrintedName = Str("printed_name"),
            OtherNames = r.TryGetProperty("other_names", out var names) && names.ValueKind == JsonValueKind.Array
                ? names.EnumerateArray().Select(n => n.GetString()?.Trim()).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList()
                : new List<string>(),
            TaxNumber = Str("tax_number"),
            PhoneOnReceipt = Str("phone"),
            InvoiceNumberLabel = Str("invoice_number_label"),
            InvoiceNumberExample = example,
            InvoiceNumberPattern = InvoiceNumbers.PatternFromExample(example),
            TotalLabel = Str("total_label"),
            DateFormat = Str("date_format"),
            Currency = Str("currency"),
            LayoutNotes = Str("layout_notes"),
        };
    }

    private static Dictionary<string, JsonElement> BuildSchema()
    {
        object S(string d) => new { type = new[] { "string", "null" }, description = d };
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[]
            {
                "is_receipt", "printed_name", "other_names", "tax_number", "phone", "invoice_number_label",
                "invoice_number_example", "total_label", "date_format", "currency", "layout_notes",
            },
            properties = new Dictionary<string, object>
            {
                ["is_receipt"] = new { type = "boolean" },
                ["printed_name"] = S("Store name as printed"),
                ["other_names"] = new { type = "array", items = new { type = "string" } },
                ["tax_number"] = S("Tax / commercial registration number"),
                ["phone"] = S("Store phone"),
                ["invoice_number_label"] = S("Label next to the invoice number"),
                ["invoice_number_example"] = S("Invoice number on this receipt"),
                ["total_label"] = S("Label next to the total"),
                ["date_format"] = S("e.g. DD/MM/YYYY"),
                ["currency"] = S("ISO code"),
                ["layout_notes"] = S("Arabic notes on what makes the receipt recognisable"),
            },
        };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(schema));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static InvoiceProfileSuggestionDto Failed(string warning) => new()
    {
        Provider = ClaudeReceiptOcrService.ProviderName,
        Warning = warning,
    };
}

/// <summary>بدون مفتاح Claude: لا تحليل آلي، يُكمل الموظف بصمة الفاتورة يدوياً.</summary>
public class ManualInvoiceProfileAnalyzer : IInvoiceProfileAnalyzer
{
    public Task<InvoiceProfileSuggestionDto> AnalyzeSampleAsync(Stream image, string storeName, ReceiptExtractionContext context) =>
        Task.FromResult(new InvoiceProfileSuggestionDto
        {
            Provider = "Manual",
            PrintedName = storeName,
            Warning = "التحليل بالذكاء الاصطناعي غير مفعّل (لم يُضبط مفتاح Claude). حُفظت الصورة، أكمل الحقول يدوياً.",
        });
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMApplication.Application.DTOs;

/// <summary>
/// ما يُحفظ في Invoice.RawAiPayload: محرك القراءة، أسباب المراجعة، وما قرأه الذكاء الاصطناعي.
/// تقرؤه شاشة مراجعة الفواتير في لوحة التحكم.
/// </summary>
public class ReceiptReviewPayload
{
    public string Provider { get; set; } = string.Empty;
    public List<string> ReviewReasons { get; set; } = new();
    public List<string> AuthenticityConcerns { get; set; } = new();
    public string? Currency { get; set; }
    public bool RequiresManualReview { get; set; }
    public List<ExtractedReceiptItemDto> Items { get; set; } = new();
    public string? Raw { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(ExtractedReceiptDto extracted, List<string> reviewReasons) =>
        JsonSerializer.Serialize(new ReceiptReviewPayload
        {
            Provider = extracted.Provider,
            ReviewReasons = reviewReasons,
            AuthenticityConcerns = extracted.AuthenticityConcerns,
            Currency = extracted.Currency,
            RequiresManualReview = extracted.RequiresManualReview,
            Items = extracted.Items,
            Raw = extracted.RawPayload,
        }, Options);

    /// <summary>يقرأ الحقل؛ يُرجع null للفواتير القديمة أو البيانات غير المتوافقة.</summary>
    public static ReceiptReviewPayload? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ReceiptReviewPayload>(json, Options); }
        catch (JsonException) { return null; }
    }
}

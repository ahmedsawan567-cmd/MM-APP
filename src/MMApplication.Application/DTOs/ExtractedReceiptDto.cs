namespace MMApplication.Application.DTOs;

public class ExtractedReceiptDto
{
    public string? MerchantName { get; set; }
    public string? InvoiceNumber { get; set; }
    /// <summary>الرقم الضريبي / السجل التجاري المطبوع على الفاتورة إن وجد.</summary>
    public string? TaxNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public double Confidence { get; set; }
    public List<ExtractedReceiptItemDto> Items { get; set; } = new();

    /// <summary>هل الصورة فاتورة شراء فعلاً؟ (false = صورة عشوائية/ليست فاتورة)</summary>
    public bool IsReceipt { get; set; } = true;

    /// <summary>عملة الفاتورة كما قرأها النظام (مثل LYD).</summary>
    public string? Currency { get; set; }

    /// <summary>معرف المتجر داخل المول إذا تعرّف عليه الذكاء الاصطناعي من قائمة المتاجر.</summary>
    public string? MatchedTenantId { get; set; }

    /// <summary>ملاحظات تشير لاحتمال تلاعب أو مشكلة (صورة شاشة، أرقام معدلة، فاتورة مقطوعة...).</summary>
    public List<string> AuthenticityConcerns { get; set; } = new();

    /// <summary>اسم محرك القراءة المستخدم (Claude / Mock ...).</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>الرد الخام من محرك القراءة (للتدقيق).</summary>
    public string? RawPayload { get; set; }

    /// <summary>true إذا تعذرت القراءة الآلية ويجب تحويل الفاتورة لمراجعة بشرية بدل رفضها.</summary>
    public bool RequiresManualReview { get; set; }
}

/// <summary>معلومات مساعدة لمحرك القراءة: قائمة متاجر المول ونوع الملف.</summary>
public class ReceiptExtractionContext
{
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public List<KnownStoreDto> KnownStores { get; set; } = new();
}

public class KnownStoreDto
{
    public string TenantId { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();

    // بصمة فاتورة المتجر (إن تعلّمها النظام)
    public string? TaxNumber { get; set; }
    public string? InvoiceNumberLabel { get; set; }
    public string? InvoiceNumberExample { get; set; }
    public string? LayoutNotes { get; set; }
}

/// <summary>ما استنتجه الذكاء الاصطناعي من صورة فاتورة نموذجية لمتجر.</summary>
public class InvoiceProfileSuggestionDto
{
    public bool IsReceipt { get; set; } = true;
    public string? PrintedName { get; set; }
    public List<string> OtherNames { get; set; } = new();
    public string? TaxNumber { get; set; }
    public string? PhoneOnReceipt { get; set; }
    public string? InvoiceNumberLabel { get; set; }
    public string? InvoiceNumberExample { get; set; }
    public string? InvoiceNumberPattern { get; set; }
    public string? TotalLabel { get; set; }
    public string? DateFormat { get; set; }
    public string? Currency { get; set; }
    public string? LayoutNotes { get; set; }
    public string Provider { get; set; } = "Mock";
    /// <summary>رسالة عند تعذّر التحليل الآلي (يُكمل الموظف الحقول يدوياً).</summary>
    public string? Warning { get; set; }
}

public class ExtractedReceiptItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal? Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Sku { get; set; }
}

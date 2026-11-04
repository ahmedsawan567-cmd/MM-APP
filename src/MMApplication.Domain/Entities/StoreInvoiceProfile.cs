namespace MMApplication.Domain.Entities;

/// <summary>
/// "بصمة" فاتورة المتجر: تفاصيل مميزة تتعلّمها المنظومة من صورة فاتورة نموذجية (بالذكاء الاصطناعي)
/// ويراجعها الموظف. تُرسل مع كل فاتورة يصورها العميل ليتعرف Claude على المتجر ويقرأ رقم الفاتورة بدقة،
/// وتُستخدم للتحقق (نمط رقم الفاتورة، الرقم الضريبي).
/// </summary>
public class StoreInvoiceProfile
{
    public string TenantId { get; set; } = string.Empty;

    /// <summary>صورة الفاتورة النموذجية التي تعلّم منها النظام.</summary>
    public string? SampleImageUrl { get; set; }

    /// <summary>اسم المتجر كما يُطبع أعلى الفاتورة.</summary>
    public string? PrintedName { get; set; }
    public string? TaxNumber { get; set; }
    public string? PhoneOnReceipt { get; set; }

    /// <summary>التسمية المطبوعة بجانب رقم الفاتورة (مثل: رقم الفاتورة، Bill No، Receipt #).</summary>
    public string? InvoiceNumberLabel { get; set; }
    /// <summary>مثال لرقم فاتورة حقيقي من هذا المتجر.</summary>
    public string? InvoiceNumberExample { get; set; }
    /// <summary>نمط رقم الفاتورة (Regex). يُولّد من المثال ويمكن تعديله.</summary>
    public string? InvoiceNumberPattern { get; set; }

    public string? TotalLabel { get; set; }
    public string? DateFormat { get; set; }
    public string? Currency { get; set; }
    /// <summary>وصف مختصر لشكل الفاتورة وما يميزها (لمساعدة الذكاء الاصطناعي).</summary>
    public string? LayoutNotes { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LearnedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

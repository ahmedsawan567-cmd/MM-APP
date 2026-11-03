namespace MMApplication.Domain.Enums;

public enum InvoiceStatus
{
    PENDING,
    PROCESSING,
    APPROVED,
    REVIEW_REQUIRED,
    REJECTED,
    RESUBMISSION_REQUESTED, // طلب إعادة تصوير الفاتورة (معتمة، مقطوعة، غير واضحة)
    DISPUTED,               // اعتراض مقدم من العميل وبانتظار التدقيق البشري
    VOIDED                  // ملغاة محاسبياً مع الاحتفاظ بالبصمة الرقمية والبيانات لمنع تكرار الرفع
}

using MMApplication.Application.DTOs;

namespace MMApplication.Application.Interfaces;

public interface IReceiptOcrService
{
    Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream);

    /// <summary>
    /// قراءة الفاتورة مع معلومات إضافية (قائمة المتاجر، نوع الملف).
    /// المحركات التي لا تحتاج هذه المعلومات تستخدم التنفيذ الافتراضي.
    /// </summary>
    Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream, ReceiptExtractionContext context)
        => ExtractReceiptDataAsync(imageStream);
}

using MMApplication.Application.DTOs;

namespace MMApplication.Application.Interfaces;

/// <summary>يحلل صورة فاتورة نموذجية لمتجر ويستخرج ما يميز فواتيره (بصمة الفاتورة).</summary>
public interface IInvoiceProfileAnalyzer
{
    Task<InvoiceProfileSuggestionDto> AnalyzeSampleAsync(Stream image, string storeName, ReceiptExtractionContext context);
}

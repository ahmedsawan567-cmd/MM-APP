using MMApplication.Application.DTOs;

namespace MMApplication.Application.Interfaces;

public interface IReceiptOcrService
{
    Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream);
}

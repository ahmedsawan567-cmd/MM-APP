namespace MMApplication.Application.DTOs;

public class ExtractedReceiptDto
{
    public string? MerchantName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public double Confidence { get; set; }
    public List<ExtractedReceiptItemDto> Items { get; set; } = new();
}

public class ExtractedReceiptItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal? Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Sku { get; set; }
}

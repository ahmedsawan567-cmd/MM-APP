namespace MMApplication.Domain.Entities;

public class InvoiceLineItem
{
    public long LineItemId { get; set; }
    public Guid InvoiceId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1.0m;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Confidence { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
}

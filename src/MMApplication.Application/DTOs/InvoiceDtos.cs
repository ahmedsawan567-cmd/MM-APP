namespace MMApplication.Application.DTOs;

public class InvoiceResponseDto
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int PointsEarned { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InvoiceUploadResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public InvoiceResponseDto? Invoice { get; set; }
    public int PointsEarned { get; set; }
    public int CurrentWalletPointsBalance { get; set; }
    public double Confidence { get; set; }
}

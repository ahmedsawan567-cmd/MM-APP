namespace MMApplication.Domain.Entities;

public class LoyaltyCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PointsMultiplier { get; set; } = 2.00m; // E.g. 2x points
    public decimal MinSpendAmount { get; set; } = 0.00m;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

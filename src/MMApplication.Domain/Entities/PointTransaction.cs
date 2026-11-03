using MMApplication.Domain.Enums;

namespace MMApplication.Domain.Entities;

public class PointTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerId { get; set; } = string.Empty;
    public int Amount { get; set; } // + for earned, - for spent
    public int BalanceAfter { get; set; }
    public PointTransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Customer Customer { get; set; } = null!;
}

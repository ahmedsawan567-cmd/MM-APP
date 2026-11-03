using MMApplication.Application.DTOs;

namespace MMApplication.Application.Interfaces;

public class LoyaltyEvaluationRequest
{
    public decimal TotalAmount { get; set; }
    public string? TenantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string CustomerTier { get; set; } = "BRONZE";
    public List<ExtractedReceiptItemDto>? Items { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
}

public class LoyaltyEvaluationResult
{
    public int TotalPointsAwarded { get; set; }
    public int BasePointsEarned { get; set; }
    public int ItemBonusPointsEarned { get; set; }
    public decimal CashbackAwarded { get; set; }
    public string PointsBreakdownJson { get; set; } = "{}";
    public List<string> Remarks { get; set; } = new();
    public List<MatchedBoosterDetail> MatchedBoosters { get; set; } = new();
    public string AppliedRuleName { get; set; } = "الافتراضية للمول";
    public string CalculationType { get; set; } = "SPEND_RATIO";
}

public class MatchedBoosterDetail
{
    public Guid BoosterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public int BonusPoints { get; set; }
    public decimal PointsMultiplier { get; set; }
}

public interface IFlexibleLoyaltyEvaluationService
{
    Task<LoyaltyEvaluationResult> EvaluateInvoicePointsAsync(LoyaltyEvaluationRequest request);
}

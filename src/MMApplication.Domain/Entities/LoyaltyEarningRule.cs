namespace MMApplication.Domain.Entities;

/// <summary>
/// قواعد احتساب واكتساب النقاط المرنة (Flexible Loyalty Earning Rules)
/// تدعم:
/// 1. نقطة ثابتة لكل زيارة/فاتورة (FIXED_PER_INVOICE)
/// 2. النقاط حسب إجمالي المبلغ (SPEND_RATIO)
/// 3. بونص الأصناف وسلة المشتريات (ITEM_BOOSTER)
/// 4. النمط الهجين الشامل (HYBRID)
/// </summary>
public class LoyaltyEarningRule
{
    public int RuleId { get; set; }
    
    // متجر محدد أو Null = قاعدة عامة على مستوى المول بالكامل
    public string? StoreTenantId { get; set; }
    
    public string RuleName { get; set; } = string.Empty;
    
    // "FIXED_PER_INVOICE", "SPEND_RATIO", "ITEM_BOOSTER", "HYBRID"
    public string CalculationType { get; set; } = "SPEND_RATIO";
    
    // النمط 1: نقاط الزيارة الثابتة
    public int FixedPointsPerInvoice { get; set; } = 0;
    
    // النمط 2: معدل النقاط لكل دينار (مثلاً: 0.10 تعني 1 نقطة لكل 10 د.ل، أو 1.00 تعني 1 نقطة لكل 1 د.ل)
    public decimal SpendRatioRate { get; set; } = 0.1000m;
    
    // الحد الأدنى لمبلغ الفاتورة لكسب النقاط
    public decimal MinInvoiceAmount { get; set; } = 0.00m;
    
    // السقف الأقصى للنقاط في الفاتورة الواحدة (Cap)
    public int? MaxPointsCap { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation
    public Tenant? StoreTenant { get; set; }
}

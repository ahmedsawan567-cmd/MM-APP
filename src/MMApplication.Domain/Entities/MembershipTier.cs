namespace MMApplication.Domain.Entities;

/// <summary>
/// مستوى عضوية (برونزي، فضي، ذهبي، VIP أو أي مستوى يضيفه المول).
/// العميل يرتقي تلقائياً عندما تتحقق شروط المستوى، ويمكن للموظف تثبيت عضوية عميل يدوياً.
/// </summary>
public class MembershipTier
{
    /// <summary>رمز ثابت بالإنجليزية (BRONZE, SILVER...). يُحفظ في حساب العميل.</summary>
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    /// <summary>المزايا كما تظهر للعميل (سطر لكل ميزة).</summary>
    public string Benefits { get; set; } = string.Empty;
    /// <summary>لون العضوية (#RRGGBB).</summary>
    public string Color { get; set; } = "#B607BC";
    /// <summary>الترتيب من الأدنى للأعلى.</summary>
    public int SortOrder { get; set; }
    /// <summary>النقاط المكتسبة المطلوبة للوصول (0 = بدون شرط).</summary>
    public int MinPoints { get; set; }
    /// <summary>المشتريات المعتمدة المطلوبة بالدينار (0 = بدون شرط).</summary>
    public decimal MinSpend { get; set; }
    /// <summary>فترة احتساب الشروط بالأشهر (0 = منذ التسجيل).</summary>
    public int QualificationMonths { get; set; }
    /// <summary>مضاعف نقاط الفواتير لأعضاء هذا المستوى (1 = بدون مضاعفة).</summary>
    public decimal PointsMultiplier { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

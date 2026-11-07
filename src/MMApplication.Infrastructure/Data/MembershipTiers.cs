using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data;

/// <summary>
/// منطق العضويات: الترقية التلقائية، مضاعف النقاط، والتقدم نحو المستوى التالي.
/// الترقية تلقائية فقط (لا يُخفَّض العميل تلقائياً)؛ التخفيض أو التثبيت يدوي من الموظف.
/// </summary>
public static class MembershipTiers
{
    /// <summary>أسماء العضويات وألوانها (للعرض السريع في الصفحات).</summary>
    private static readonly ConcurrentDictionary<string, (string Name, string Color)> Names = new(StringComparer.OrdinalIgnoreCase);

    public static string NameOf(string? code) =>
        code != null && Names.TryGetValue(code, out var t) ? t.Name : (code?.ToUpperInvariant()) switch
        {
            "VIP" => "VIP", "GOLD" => "ذهبي", "SILVER" => "فضي", "PLATINUM" => "بلاتيني", _ => "برونزي",
        };

    public static string? ColorOf(string? code) => code != null && Names.TryGetValue(code, out var t) ? t.Color : null;

    private static void Remember(IEnumerable<MembershipTier> tiers)
    {
        foreach (var t in tiers) Names[t.Code] = (t.NameAr, t.Color);
    }

    public static readonly MembershipTier[] Defaults =
    [
        new() { Code = "BRONZE", NameAr = "برونزي", SortOrder = 1, Color = "#B45309", PointsMultiplier = 1m, Benefits = "كسب النقاط من كل فاتورة\nاستبدال النقاط بمكافآت المول" },
        new() { Code = "SILVER", NameAr = "فضي", SortOrder = 2, Color = "#64748B", MinPoints = 500, PointsMultiplier = 1.25m, Benefits = "نقاط إضافية 25% على كل فاتورة\nعروض خاصة لأعضاء الفضية" },
        new() { Code = "GOLD", NameAr = "ذهبي", SortOrder = 3, Color = "#CA8A04", MinPoints = 1500, PointsMultiplier = 1.5m, Benefits = "نقاط إضافية 50% على كل فاتورة\nأولوية في العروض والفعاليات" },
        new() { Code = "VIP", NameAr = "VIP", SortOrder = 4, Color = "#7E22CE", MinPoints = 5000, PointsMultiplier = 1.5m, Benefits = "نقاط إضافية 50%\nهدايا ودعوات حصرية\nخدمة عملاء مخصصة" },
    ];

    public static async Task EnsureDefaultsAsync(AppDbContext db)
    {
        if (!await db.MembershipTiers.AnyAsync())
        {
            db.MembershipTiers.AddRange(Defaults.Select(d => new MembershipTier
            {
                Code = d.Code, NameAr = d.NameAr, SortOrder = d.SortOrder, Color = d.Color, MinPoints = d.MinPoints,
                PointsMultiplier = d.PointsMultiplier, Benefits = d.Benefits,
            }));
            try { await db.SaveChangesAsync(); }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException) { db.ChangeTracker.Clear(); }
        }
        Remember(await db.MembershipTiers.AsNoTracking().ToListAsync());
    }

    public static async Task<List<MembershipTier>> AllAsync(AppDbContext db)
    {
        var tiers = await db.MembershipTiers.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync();
        if (tiers.Count == 0) tiers = Defaults.ToList();
        Remember(tiers);
        return tiers;
    }

    /// <summary>مضاعف نقاط العضوية (1 إن لم توجد).</summary>
    public static async Task<decimal> MultiplierAsync(AppDbContext db, string? code)
    {
        if (string.IsNullOrEmpty(code)) return 1m;
        var tier = (await AllAsync(db)).FirstOrDefault(t => t.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        return tier?.IsActive == true ? Math.Max(1m, tier.PointsMultiplier) : 1m;
    }

    public record Progress(int Points, decimal Spent, MembershipTier? Current, MembershipTier? Next, int PointsToNext, decimal SpendToNext, decimal Percent);

    /// <summary>النقاط والمشتريات المحتسبة للعضوية ضمن فترة المستوى.</summary>
    private static async Task<(int Points, decimal Spent)> TotalsAsync(AppDbContext db, string customerId, int months)
    {
        var q = db.Invoices.AsNoTracking().Where(i => i.CustomerId == customerId && i.ProcessingStatus == InvoiceStatus.APPROVED);
        if (months > 0)
        {
            var since = DateTime.UtcNow.AddMonths(-months);
            q = q.Where(i => i.CreatedAt >= since);
        }
        var points = await q.SumAsync(i => (int?)i.PointsAwarded) ?? 0;
        var spent = await q.SumAsync(i => i.TotalAmount) ?? 0m;
        return (points, spent);
    }

    private static async Task<bool> QualifiesAsync(AppDbContext db, string customerId, MembershipTier t)
    {
        if (t.MinPoints <= 0 && t.MinSpend <= 0) return true;
        var (points, spent) = await TotalsAsync(db, customerId, t.QualificationMonths);
        return (t.MinPoints <= 0 || points >= t.MinPoints) && (t.MinSpend <= 0 || spent >= t.MinSpend);
    }

    public static async Task<Progress> ProgressAsync(AppDbContext db, Customer customer)
    {
        var tiers = (await AllAsync(db)).Where(t => t.IsActive).ToList();
        var current = tiers.FirstOrDefault(t => t.Code.Equals(customer.LoyaltyTier, StringComparison.OrdinalIgnoreCase));
        var next = tiers.Where(t => t.SortOrder > (current?.SortOrder ?? 0)).OrderBy(t => t.SortOrder).FirstOrDefault();
        var (points, spent) = await TotalsAsync(db, customer.CustomerId, next?.QualificationMonths ?? 0);
        if (next == null) return new Progress(points, spent, current, null, 0, 0, 100);

        var pointsTo = Math.Max(0, next.MinPoints - points);
        var spendTo = Math.Max(0, next.MinSpend - spent);
        var parts = new List<decimal>();
        if (next.MinPoints > 0) parts.Add(Math.Min(1m, points / (decimal)next.MinPoints));
        if (next.MinSpend > 0) parts.Add(Math.Min(1m, spent / next.MinSpend));
        var pct = parts.Count == 0 ? 100 : Math.Round(parts.Min() * 100, 0);
        return new Progress(points, spent, current, next, pointsTo, spendTo, pct);
    }

    /// <summary>
    /// يرقّي العميل لأعلى مستوى تتحقق شروطه (إن لم تكن عضويته مثبّتة). يضيف الإشعار والسجل للسياق ويحفظ.
    /// يرجع رمز العضوية الجديدة إن تغيّرت.
    /// </summary>
    public static async Task<string?> EvaluateAsync(AppDbContext db, string customerId, string actor = "النظام")
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer == null || customer.TierLocked || customer.IsArchived) return null;

        var tiers = (await AllAsync(db)).Where(t => t.IsActive).OrderByDescending(t => t.SortOrder).ToList();
        var currentOrder = tiers.FirstOrDefault(t => t.Code.Equals(customer.LoyaltyTier, StringComparison.OrdinalIgnoreCase))?.SortOrder ?? 0;
        foreach (var t in tiers)
        {
            if (t.SortOrder <= currentOrder) break; // ترقية فقط
            if (!await QualifiesAsync(db, customerId, t)) continue;

            var old = customer.LoyaltyTier;
            customer.LoyaltyTier = t.Code;
            customer.TierUpdatedAt = DateTime.UtcNow;
            Notifier.Customer(db, customerId, "TIER_UPGRADE", $"مبروك! أصبحت عضواً {t.NameAr}",
                string.IsNullOrWhiteSpace(t.Benefits) ? "استمتع بمزايا عضويتك الجديدة." : "مزاياك الجديدة: " + t.Benefits.Replace("\n", "، "));
            db.AuditLogs.Add(new AuditLog
            {
                PerformedBy = actor, Action = "CUSTOMER_TIER_CHANGE", EntityName = "Customer", EntityId = customerId,
                Details = $"ترقية {customer.FullName} ({customer.MembershipNumber}) من {NameOf(old)} إلى {t.NameAr}", CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return t.Code;
        }
        return null;
    }

    /// <summary>إعادة تقييم كل العملاء (بعد تعديل شروط العضويات). يرجع عدد من تمت ترقيتهم.</summary>
    public static async Task<int> EvaluateAllAsync(AppDbContext db, string actor)
    {
        var ids = await db.Customers.AsNoTracking().Where(c => !c.IsArchived && !c.TierLocked).Select(c => c.CustomerId).ToListAsync();
        var upgraded = 0;
        foreach (var id in ids)
            if (await EvaluateAsync(db, id, actor) != null) upgraded++;
        return upgraded;
    }
}

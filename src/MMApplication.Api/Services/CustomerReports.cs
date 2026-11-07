using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Services;

/// <summary>تقارير العملاء: الجدد، الأكثر نقاطاً، الأكثر شراءً، الأكثر نشاطاً، الأرصدة، غير النشطين، والأكثر استبدالاً.</summary>
public static class CustomerReports
{
    public record ReportType(string Key, string Title, string Description, string MetricLabel, string? SecondLabel, bool UsesPeriod);

    public static readonly ReportType[] Types =
    [
        new("new", "العملاء الجدد", "الحسابات التي سُجّلت خلال الفترة", "الفواتير", "النقاط الحالية", true),
        new("points", "الأكثر جمعاً للنقاط", "ترتيب العملاء حسب النقاط التي كسبوها خلال الفترة", "النقاط المكتسبة", "الفواتير", true),
        new("spend", "الأكثر شراءً", "ترتيب العملاء حسب مشترياتهم المعتمدة خلال الفترة", "المشتريات (د.ل)", "الفواتير", true),
        new("active", "الأكثر نشاطاً", "ترتيب العملاء حسب عدد الفواتير خلال الفترة", "الفواتير", "المشتريات (د.ل)", true),
        new("redeem", "الأكثر استبدالاً للمكافآت", "العملاء الذين استبدلوا نقاطاً بمكافآت خلال الفترة", "النقاط المستبدلة", "القسائم", true),
        new("balance", "أرصدة النقاط", "العملاء حسب رصيدهم الحالي من النقاط", "الرصيد الحالي", "الفواتير (الكل)", false),
        new("inactive", "العملاء غير النشطين", "عملاء لم يرفعوا أي فاتورة خلال الفترة", "آخر فاتورة", "الرصيد الحالي", true),
    ];

    public static readonly (string Key, string Label)[] Periods =
    [
        ("this_month", "هذا الشهر"), ("last_month", "الشهر الماضي"), ("last_7", "آخر 7 أيام"), ("last_30", "آخر 30 يوماً"),
        ("last_90", "آخر 3 أشهر"), ("this_year", "هذه السنة"), ("last_year", "السنة الماضية"), ("all", "منذ البداية"), ("custom", "فترة محددة"),
    ];

    /// <summary>بداية الفترة (شاملة) ونهايتها (غير شاملة) بتوقيت الخادم المحلي محوّلة لـ UTC.</summary>
    public static (DateTime From, DateTime To, string Label) Range(string? period, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        (DateTime f, DateTime t) = (period ?? "this_month") switch
        {
            "last_month" => (monthStart.AddMonths(-1), monthStart),
            "last_7" => (today.AddDays(-6), today.AddDays(1)),
            "last_30" => (today.AddDays(-29), today.AddDays(1)),
            "last_90" => (today.AddDays(-89), today.AddDays(1)),
            "this_year" => (new DateTime(today.Year, 1, 1), today.AddDays(1)),
            "last_year" => (new DateTime(today.Year - 1, 1, 1), new DateTime(today.Year, 1, 1)),
            "all" => (new DateTime(2000, 1, 1), today.AddDays(1)),
            "custom" when from.HasValue => (from.Value.Date, (to ?? today).Date.AddDays(1)),
            _ => (monthStart, today.AddDays(1)),
        };
        if (t <= f) t = f.AddDays(1);
        var label = period == "all" ? "منذ البداية" : $"من {f:yyyy/MM/dd} إلى {t.AddDays(-1):yyyy/MM/dd}";
        return (DateTime.SpecifyKind(f, DateTimeKind.Local).ToUniversalTime(), DateTime.SpecifyKind(t, DateTimeKind.Local).ToUniversalTime(), label);
    }

    public record Row(int Rank, string CustomerId, string? MembershipNumber, string FullName, string Phone, string Tier, DateTime RegisteredAt,
        decimal Metric, decimal? Second, DateTime? LastActivity);

    public record Result(ReportType Type, string PeriodLabel, List<Row> Rows, int TotalCustomers, decimal MetricTotal);

    public static async Task<Result> RunAsync(AppDbContext db, string? typeKey, DateTime from, DateTime to, string periodLabel, string? tier, string? scopeTenantId)
    {
        var type = Types.FirstOrDefault(t => t.Key == typeKey) ?? Types[0];
        var customers = db.Customers.AsNoTracking().Where(c => !c.IsArchived);
        if (!string.IsNullOrWhiteSpace(tier) && tier != "ALL") customers = customers.Where(c => c.LoyaltyTier == tier);
        var invoices = db.Invoices.AsNoTracking().Where(i => scopeTenantId == null || i.TenantId == scopeTenantId);
        var approved = invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED);
        var inPeriod = approved.Where(i => i.CreatedAt >= from && i.CreatedAt < to);

        var balances = db.CustomerWallets.AsNoTracking().Where(w => w.BalanceType == BalanceType.POINTS);
        var baseList = await customers.Select(c => new { c.CustomerId, c.MembershipNumber, c.FullName, c.PhoneNumber, c.LoyaltyTier, c.RegisteredAt }).ToListAsync();
        var ids = baseList.Select(c => c.CustomerId).ToHashSet();

        List<(string Id, decimal Metric, decimal? Second, DateTime? Last)> data;
        switch (type.Key)
        {
            case "new":
            {
                var newIds = baseList.Where(c => c.RegisteredAt >= from && c.RegisteredAt < to).Select(c => c.CustomerId).ToList();
                var counts = await inPeriod.Where(i => newIds.Contains(i.CustomerId)).GroupBy(i => i.CustomerId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
                var bal = await balances.Where(w => newIds.Contains(w.CustomerId)).Select(w => new { w.CustomerId, w.CurrentBalance }).ToListAsync();
                data = newIds.Select(id => (id, (decimal)(counts.FirstOrDefault(x => x.Key == id)?.N ?? 0),
                    (decimal?)(bal.FirstOrDefault(b => b.CustomerId == id)?.CurrentBalance ?? 0), (DateTime?)null))
                    .OrderByDescending(x => baseList.First(c => c.CustomerId == x.id).RegisteredAt).ToList();
                break;
            }
            case "points":
            case "spend":
            case "active":
            {
                var g = await inPeriod.GroupBy(i => i.CustomerId)
                    .Select(x => new { x.Key, Points = x.Sum(i => i.PointsAwarded), Spent = x.Sum(i => i.TotalAmount ?? 0), N = x.Count(), Last = x.Max(i => i.CreatedAt) })
                    .ToListAsync();
                data = g.Where(x => ids.Contains(x.Key)).Select(x => type.Key switch
                {
                    "points" => (x.Key, (decimal)x.Points, (decimal?)x.N, (DateTime?)x.Last),
                    "spend" => (x.Key, x.Spent, (decimal?)x.N, (DateTime?)x.Last),
                    _ => (x.Key, (decimal)x.N, (decimal?)x.Spent, (DateTime?)x.Last),
                }).Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).ToList();
                break;
            }
            case "redeem":
            {
                var g = await db.Redemptions.AsNoTracking().Where(r => r.RedeemedAt >= from && r.RedeemedAt < to)
                    .GroupBy(r => r.CustomerId).Select(x => new { x.Key, Points = x.Sum(r => r.PointsSpent), N = x.Count(), Last = x.Max(r => r.RedeemedAt) }).ToListAsync();
                data = g.Where(x => ids.Contains(x.Key)).Select(x => (x.Key, (decimal)x.Points, (decimal?)x.N, (DateTime?)x.Last))
                    .OrderByDescending(x => x.Item2).ToList();
                break;
            }
            case "balance":
            {
                var bal = await balances.Select(w => new { w.CustomerId, w.CurrentBalance }).ToListAsync();
                var counts = await approved.GroupBy(i => i.CustomerId).Select(g => new { g.Key, N = g.Count(), Last = g.Max(i => i.CreatedAt) }).ToListAsync();
                data = bal.Where(b => ids.Contains(b.CustomerId) && b.CurrentBalance > 0)
                    .Select(b => { var c = counts.FirstOrDefault(x => x.Key == b.CustomerId); return (b.CustomerId, b.CurrentBalance, (decimal?)(c?.N ?? 0), c?.Last); })
                    .OrderByDescending(x => x.Item2).ToList();
                break;
            }
            default: // inactive
            {
                var activeIds = (await inPeriod.Select(i => i.CustomerId).Distinct().ToListAsync()).ToHashSet();
                var last = await approved.GroupBy(i => i.CustomerId).Select(g => new { g.Key, Last = g.Max(i => i.CreatedAt) }).ToListAsync();
                var bal = await balances.Select(w => new { w.CustomerId, w.CurrentBalance }).ToListAsync();
                data = baseList.Where(c => c.RegisteredAt < to && !activeIds.Contains(c.CustomerId))
                    .Select(c => (c.CustomerId, 0m, (decimal?)(bal.FirstOrDefault(b => b.CustomerId == c.CustomerId)?.CurrentBalance ?? 0),
                        last.FirstOrDefault(l => l.Key == c.CustomerId)?.Last))
                    .OrderBy(x => x.Item4 ?? DateTime.MinValue).ToList();
                break;
            }
        }

        var lookup = baseList.ToDictionary(c => c.CustomerId);
        var rows = data.Select((x, i) =>
        {
            var c = lookup[x.Id];
            return new Row(i + 1, c.CustomerId, c.MembershipNumber, c.FullName, c.PhoneNumber, c.LoyaltyTier, c.RegisteredAt, x.Metric, x.Second, x.Last);
        }).ToList();
        return new Result(type, periodLabel, rows, rows.Count, type.Key is "inactive" or "new" ? rows.Count : rows.Sum(r => r.Metric));
    }
}

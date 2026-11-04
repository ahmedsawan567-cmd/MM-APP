using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Services;

public class ReportFilter
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }       // شامل (نهاية اليوم)
    public string? TenantId { get; set; }

    /// <summary>يبني الفلتر من مدخلات الصفحة مع قيم افتراضية آمنة (آخر 30 يوماً، وحد أقصى سنة).</summary>
    public static ReportFilter From_(string? range, DateTime? from, DateTime? to, string? tenantId)
    {
        var today = DateTime.UtcNow.Date;
        DateTime start, end = today;
        switch (range)
        {
            case "7d": start = today.AddDays(-6); break;
            case "90d": start = today.AddDays(-89); break;
            case "month": start = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc); break;
            case "custom" when from.HasValue:
                start = from.Value.Date;
                end = (to ?? today).Date;
                break;
            default: start = today.AddDays(-29); break;
        }
        if (end < start) (start, end) = (end, start);
        if ((end - start).TotalDays > 366) start = end.AddDays(-366);
        return new ReportFilter
        {
            From = DateTime.SpecifyKind(start, DateTimeKind.Utc),
            To = DateTime.SpecifyKind(end, DateTimeKind.Utc),
            TenantId = string.IsNullOrWhiteSpace(tenantId) || tenantId == "ALL" ? null : tenantId,
        };
    }
}

public class ReportData
{
    public ReportFilter Filter { get; set; } = new();
    public string? TenantName { get; set; }

    public decimal ApprovedSales { get; set; }
    public int TotalInvoices { get; set; }
    public int ApprovedInvoices { get; set; }
    public int RejectedInvoices { get; set; }
    public int InReviewInvoices { get; set; }
    public int AutoApprovedInvoices { get; set; }
    public int PointsIssued { get; set; }
    public int PointsRedeemed { get; set; }
    public int VouchersIssued { get; set; }
    public int VouchersUsed { get; set; }
    public int NewCustomers { get; set; }
    public int ActiveCustomers { get; set; }
    public decimal AverageInvoice => ApprovedInvoices > 0 ? Math.Round(ApprovedSales / ApprovedInvoices, 2) : 0;
    public decimal AutoApprovalRate => ApprovedInvoices > 0 ? Math.Round(AutoApprovedInvoices * 100m / ApprovedInvoices, 1) : 0;

    public List<DailyRow> Daily { get; set; } = new();
    public List<StoreRow> Stores { get; set; } = new();
    public List<CustomerRow> TopCustomers { get; set; } = new();
    public List<RewardRow> Rewards { get; set; } = new();
    public List<ReasonRow> RejectionReasons { get; set; } = new();
    public List<InvoiceRow> Invoices { get; set; } = new();

    public record DailyRow(DateTime Date, int Invoices, int Approved, decimal Sales, int Points);
    public record StoreRow(string TenantId, string Name, string Category, int Invoices, int Approved, int Rejected, decimal Sales, int Points, decimal SharePercent);
    public record CustomerRow(string Id, string Name, string Phone, string Tier, int Invoices, decimal Sales, int Points);
    public record RewardRow(string Title, int Redemptions, int PointsSpent, int Used);
    public record ReasonRow(string Reason, int Count);
    public record InvoiceRow(DateTime CreatedAt, string Store, string Customer, string Phone, string Number, DateTime? InvoiceDate, decimal? Amount, int Points, string Status, string? Reason);
}

/// <summary>تقارير لوحة التحكم: المبيعات، النقاط، العملاء، المكافآت — مع تصدير Excel.</summary>
public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

    public async Task<ReportData> BuildAsync(ReportFilter f)
    {
        var fromUtc = f.From;
        var toExclusive = f.To.AddDays(1);

        var invoiceQuery = _db.Invoices.AsNoTracking()
            .Include(i => i.Tenant).Include(i => i.Customer)
            .Where(i => i.CreatedAt >= fromUtc && i.CreatedAt < toExclusive);
        if (f.TenantId != null) invoiceQuery = invoiceQuery.Where(i => i.TenantId == f.TenantId);
        var invoices = await invoiceQuery.OrderByDescending(i => i.CreatedAt).ToListAsync();

        var approved = invoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).ToList();
        var approvedIds = approved.Select(i => i.InvoiceId.ToString()).ToList();
        var manuallyApproved = approvedIds.Count == 0 ? new HashSet<string>() : (await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "INVOICE_APPROVE" && a.EntityId != null && approvedIds.Contains(a.EntityId))
            .Select(a => a.EntityId!)
            .ToListAsync()).ToHashSet();

        var data = new ReportData
        {
            Filter = f,
            TenantName = f.TenantId == null ? null : await _db.Tenants.Where(t => t.TenantId == f.TenantId).Select(t => t.NameAr).FirstOrDefaultAsync(),
            TotalInvoices = invoices.Count,
            ApprovedInvoices = approved.Count,
            RejectedInvoices = invoices.Count(i => i.ProcessingStatus == InvoiceStatus.REJECTED),
            InReviewInvoices = invoices.Count(i => i.ProcessingStatus is InvoiceStatus.REVIEW_REQUIRED or InvoiceStatus.PENDING
                or InvoiceStatus.PROCESSING or InvoiceStatus.DISPUTED or InvoiceStatus.RESUBMISSION_REQUESTED),
            AutoApprovedInvoices = approved.Count(i => !manuallyApproved.Contains(i.InvoiceId.ToString())),
            ApprovedSales = approved.Sum(i => i.TotalAmount ?? 0m),
            PointsIssued = approved.Sum(i => i.PointsAwarded),
            ActiveCustomers = invoices.Select(i => i.CustomerId).Distinct().Count(),
        };

        // اليومي
        var days = (int)(f.To - f.From).TotalDays + 1;
        data.Daily = Enumerable.Range(0, days).Select(d =>
        {
            var day = f.From.AddDays(d);
            var dayInvoices = invoices.Where(i => i.CreatedAt.Date == day).ToList();
            var dayApproved = dayInvoices.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).ToList();
            return new ReportData.DailyRow(day, dayInvoices.Count, dayApproved.Count, dayApproved.Sum(i => i.TotalAmount ?? 0m), dayApproved.Sum(i => i.PointsAwarded));
        }).ToList();

        // المتاجر
        data.Stores = invoices
            .GroupBy(i => i.TenantId ?? "-")
            .Select(g =>
            {
                var first = g.First();
                var sales = g.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.TotalAmount ?? 0m);
                return new ReportData.StoreRow(
                    g.Key,
                    first.Tenant?.NameAr ?? "متاجر غير معروفة",
                    first.Tenant?.Category ?? "-",
                    g.Count(),
                    g.Count(i => i.ProcessingStatus == InvoiceStatus.APPROVED),
                    g.Count(i => i.ProcessingStatus == InvoiceStatus.REJECTED),
                    sales,
                    g.Where(i => i.ProcessingStatus == InvoiceStatus.APPROVED).Sum(i => i.PointsAwarded),
                    data.ApprovedSales > 0 ? Math.Round(sales * 100 / data.ApprovedSales, 1) : 0);
            })
            .OrderByDescending(s => s.Sales).ThenByDescending(s => s.Invoices)
            .ToList();

        // أفضل العملاء
        data.TopCustomers = approved
            .GroupBy(i => i.CustomerId)
            .Select(g => new ReportData.CustomerRow(
                g.Key,
                g.First().Customer?.FullName ?? g.Key,
                g.First().Customer?.PhoneNumber ?? "",
                g.First().Customer?.LoyaltyTier ?? "",
                g.Count(),
                g.Sum(i => i.TotalAmount ?? 0m),
                g.Sum(i => i.PointsAwarded)))
            .OrderByDescending(c => c.Sales)
            .Take(20)
            .ToList();

        data.RejectionReasons = invoices
            .Where(i => i.ProcessingStatus == InvoiceStatus.REJECTED && !string.IsNullOrWhiteSpace(i.RejectionReason))
            .GroupBy(i => i.RejectionReason!.Trim())
            .Select(g => new ReportData.ReasonRow(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .Take(8)
            .ToList();

        // المكافآت والعملاء الجدد على مستوى المول فقط (ليست مرتبطة بمتجر واحد)
        if (f.TenantId == null)
        {
            var redemptions = await _db.Redemptions.AsNoTracking().Include(r => r.Reward)
                .Where(r => r.RedeemedAt >= fromUtc && r.RedeemedAt < toExclusive)
                .ToListAsync();
            data.PointsRedeemed = redemptions.Where(r => r.Status != RedemptionStatus.CANCELLED).Sum(r => r.PointsSpent);
            data.VouchersIssued = redemptions.Count;
            data.VouchersUsed = redemptions.Count(r => r.Status == RedemptionStatus.USED);
            data.Rewards = redemptions
                .GroupBy(r => r.Reward?.Title ?? "مكافأة محذوفة")
                .Select(g => new ReportData.RewardRow(g.Key, g.Count(), g.Sum(r => r.PointsSpent), g.Count(r => r.Status == RedemptionStatus.USED)))
                .OrderByDescending(r => r.Redemptions)
                .ToList();
            data.NewCustomers = await _db.Customers.CountAsync(c => c.RegisteredAt >= fromUtc && c.RegisteredAt < toExclusive);
        }

        data.Invoices = invoices.Select(i => new ReportData.InvoiceRow(
            i.CreatedAt,
            i.Tenant?.NameAr ?? i.ExtractedMerchant ?? "-",
            i.Customer?.FullName ?? i.CustomerId,
            i.Customer?.PhoneNumber ?? "",
            i.InvoiceNumber ?? "",
            i.InvoiceDate,
            i.TotalAmount,
            i.PointsAwarded,
            StatusAr(i.ProcessingStatus),
            i.RejectionReason)).ToList();

        return data;
    }

    public static string StatusAr(InvoiceStatus s) => s switch
    {
        InvoiceStatus.APPROVED => "معتمدة",
        InvoiceStatus.REJECTED => "مرفوضة",
        InvoiceStatus.DISPUTED => "اعتراض",
        InvoiceStatus.RESUBMISSION_REQUESTED => "بانتظار صورة جديدة",
        InvoiceStatus.VOIDED => "ملغاة",
        _ => "قيد المراجعة",
    };

    public static byte[] ToExcel(ReportData d)
    {
        using var wb = new XLWorkbook();
        wb.RightToLeft = true;

        var title = $"تقرير المرأة مول — من {d.Filter.From:yyyy-MM-dd} إلى {d.Filter.To:yyyy-MM-dd}" +
                    (d.TenantName != null ? $" — {d.TenantName}" : "");

        var s = wb.AddWorksheet("الملخص");
        s.Cell(1, 1).Value = title;
        s.Cell(1, 1).Style.Font.Bold = true;
        s.Cell(1, 1).Style.Font.FontSize = 14;
        var kpis = new (string, XLCellValue)[]
        {
            ("المبيعات المعتمدة (د.ل)", d.ApprovedSales),
            ("عدد الفواتير", d.TotalInvoices),
            ("فواتير معتمدة", d.ApprovedInvoices),
            ("معتمدة تلقائياً بالذكاء الاصطناعي", d.AutoApprovedInvoices),
            ("نسبة الاعتماد التلقائي %", d.AutoApprovalRate),
            ("فواتير مرفوضة", d.RejectedInvoices),
            ("قيد المراجعة", d.InReviewInvoices),
            ("متوسط الفاتورة (د.ل)", d.AverageInvoice),
            ("النقاط الممنوحة", d.PointsIssued),
            ("النقاط المستبدلة", d.PointsRedeemed),
            ("قسائم صادرة", d.VouchersIssued),
            ("قسائم مستخدمة", d.VouchersUsed),
            ("عملاء جدد", d.NewCustomers),
            ("عملاء نشطون", d.ActiveCustomers),
        };
        for (var i = 0; i < kpis.Length; i++)
        {
            s.Cell(i + 3, 1).Value = kpis[i].Item1;
            s.Cell(i + 3, 2).Value = kpis[i].Item2;
        }
        s.Columns().AdjustToContents();

        AddTable(wb, "يومي", new[] { "التاريخ", "الفواتير", "المعتمدة", "المبيعات (د.ل)", "النقاط" },
            d.Daily.Select(r => new XLCellValue[] { r.Date, r.Invoices, r.Approved, r.Sales, r.Points }));
        AddTable(wb, "المتاجر", new[] { "المتجر", "الفئة", "الفواتير", "المعتمدة", "المرفوضة", "المبيعات (د.ل)", "النقاط", "الحصة %" },
            d.Stores.Select(r => new XLCellValue[] { r.Name, r.Category, r.Invoices, r.Approved, r.Rejected, r.Sales, r.Points, r.SharePercent }));
        AddTable(wb, "أفضل العملاء", new[] { "العميل", "الهاتف", "المستوى", "الفواتير", "المشتريات (د.ل)", "النقاط" },
            d.TopCustomers.Select(r => new XLCellValue[] { r.Name, r.Phone, r.Tier, r.Invoices, r.Sales, r.Points }));
        if (d.Rewards.Count > 0)
        {
            AddTable(wb, "المكافآت", new[] { "المكافأة", "مرات الاستبدال", "النقاط", "قسائم مستخدمة" },
                d.Rewards.Select(r => new XLCellValue[] { r.Title, r.Redemptions, r.PointsSpent, r.Used }));
        }
        AddTable(wb, "الفواتير", new[] { "تاريخ الرفع", "المتجر", "العميل", "الهاتف", "رقم الفاتورة", "تاريخ الفاتورة", "المبلغ (د.ل)", "النقاط", "الحالة", "سبب الرفض" },
            d.Invoices.Select(r => new XLCellValue[]
            {
                r.CreatedAt, r.Store, r.Customer, r.Phone, r.Number,
                r.InvoiceDate.HasValue ? r.InvoiceDate.Value : Blank.Value,
                r.Amount.HasValue ? r.Amount.Value : Blank.Value,
                r.Points, r.Status, r.Reason ?? "",
            }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AddTable(XLWorkbook wb, string name, string[] headers, IEnumerable<XLCellValue[]> rows)
    {
        var ws = wb.AddWorksheet(name);
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++) ws.Cell(r, c + 1).Value = row[c];
            r++;
        }
        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#064E3B");
        header.Style.Font.FontColor = XLColor.White;
        ws.SheetView.FreezeRows(1);
        foreach (var cell in ws.CellsUsed(c => c.DataType == XLDataType.DateTime))
        {
            cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        }
        foreach (var cell in ws.CellsUsed(c => c.DataType == XLDataType.Number && c.Address.RowNumber > 1))
        {
            cell.Style.NumberFormat.Format = "#,##0.##";
        }
        ws.Columns().AdjustToContents(1, Math.Min(r, 500));
    }
}

using Microsoft.EntityFrameworkCore;
using MMApplication.Api.Services;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Tests;

public class ReportServiceTests
{
    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Tenants.Add(new Tenant { TenantId = "T1", NameAr = "متجر 1", NameEn = "S1", Category = "Fashion", UnitNumber = "G-1" });
        db.Tenants.Add(new Tenant { TenantId = "T2", NameAr = "متجر 2", NameEn = "S2", Category = "Cafe", UnitNumber = "G-2" });
        db.Customers.Add(new Customer { CustomerId = "C1", FullName = "أ", PhoneNumber = "0911", RegisteredAt = DateTime.UtcNow });
        db.Customers.Add(new Customer { CustomerId = "C2", FullName = "ب", PhoneNumber = "0922", RegisteredAt = DateTime.UtcNow.AddDays(-100) });

        void Inv(string c, string t, decimal amount, int points, InvoiceStatus s, int daysAgo, string? reason = null) =>
            db.Invoices.Add(new Invoice
            {
                CustomerId = c, TenantId = t, InvoiceNumber = Guid.NewGuid().ToString()[..6], TotalAmount = amount,
                PointsAwarded = points, ProcessingStatus = s, CreatedAt = DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(10),
                RejectionReason = reason, ReceiptImageUrl = "/x.jpg",
            });

        Inv("C1", "T1", 100, 10, InvoiceStatus.APPROVED, 1);
        Inv("C1", "T1", 300, 30, InvoiceStatus.APPROVED, 2);
        Inv("C2", "T2", 50, 5, InvoiceStatus.APPROVED, 3);
        Inv("C2", "T2", 999, 0, InvoiceStatus.REJECTED, 3, "مكررة");
        Inv("C2", "T2", 70, 0, InvoiceStatus.REVIEW_REQUIRED, 0);
        Inv("C1", "T1", 5000, 500, InvoiceStatus.APPROVED, 60); // خارج الفترة
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Build_ComputesKpisForRange()
    {
        using var db = CreateDb();
        var data = await new ReportService(db).BuildAsync(ReportFilter.From_("30d", null, null, null));

        Assert.Equal(450m, data.ApprovedSales);
        Assert.Equal(5, data.TotalInvoices);
        Assert.Equal(3, data.ApprovedInvoices);
        Assert.Equal(1, data.RejectedInvoices);
        Assert.Equal(1, data.InReviewInvoices);
        Assert.Equal(45, data.PointsIssued);
        Assert.Equal(1, data.NewCustomers);
        Assert.Equal(150m, data.AverageInvoice);
        Assert.Equal(30, data.Daily.Count);
        Assert.Equal("T1", data.Stores.First().TenantId);
        Assert.Equal(88.9m, data.Stores.First().SharePercent);
        Assert.Equal("C1", data.TopCustomers.First().Id);
        Assert.Single(data.RejectionReasons);
    }

    [Fact]
    public async Task Build_FiltersByStore()
    {
        using var db = CreateDb();
        var data = await new ReportService(db).BuildAsync(ReportFilter.From_("30d", null, null, "T2"));

        Assert.Equal(50m, data.ApprovedSales);
        Assert.Equal(3, data.TotalInvoices);
        Assert.Equal("متجر 2", data.TenantName);
    }

    [Fact]
    public void Filter_ClampsLongRangesAndSwapsDates()
    {
        var f = ReportFilter.From_("custom", new DateTime(2026, 5, 1), new DateTime(2024, 1, 1), null);
        Assert.True(f.From <= f.To);
        Assert.True((f.To - f.From).TotalDays <= 366);
    }

    [Fact]
    public async Task ToExcel_ProducesWorkbook()
    {
        using var db = CreateDb();
        var data = await new ReportService(db).BuildAsync(ReportFilter.From_("30d", null, null, null));
        var bytes = ReportService.ToExcel(data);

        Assert.True(bytes.Length > 1000);
        Assert.Equal((byte)'P', bytes[0]); // ملف ZIP (xlsx)
        Assert.Equal((byte)'K', bytes[1]);
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data;

/// <summary>
/// إعدادات القبول التلقائي للفواتير. تُعدَّل من لوحة التحكم (صفحة الإعدادات) وتُحفظ في القاعدة،
/// وإن لم تُضبط تُقرأ من appsettings (قسم InvoiceReview) ثم القيم الافتراضية.
/// </summary>
public record ReviewSettings(double MinConfidence, decimal ReviewAmountAbove, int MaxDailyUploads, bool RequireKnownStore)
{
    public const string MinConfidenceKey = "InvoiceReview.MinAutoApproveConfidence";
    public const string AmountKey = "InvoiceReview.ReviewAmountAbove";
    public const string DailyKey = "InvoiceReview.MaxDailyUploadsPerCustomer";
    public const string KnownStoreKey = "InvoiceReview.RequireKnownStore";

    public static async Task<ReviewSettings> LoadAsync(AppDbContext db, IConfiguration? config)
    {
        var stored = await db.SystemSettings.AsNoTracking()
            .Where(s => s.Key.StartsWith("InvoiceReview."))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        // القيمة المحفوظة من لوحة التحكم أولاً، ثم appsettings
        string? Raw(string key, string configKey) => stored.TryGetValue(key, out var v) ? v : config?[configKey];
        double D(string key, string configKey, double fallback) =>
            double.TryParse(Raw(key, configKey), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        decimal M(string key, string configKey, decimal fallback) =>
            decimal.TryParse(Raw(key, configKey), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        int I(string key, string configKey, int fallback) =>
            int.TryParse(Raw(key, configKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        bool B(string key, string configKey, bool fallback) =>
            bool.TryParse(Raw(key, configKey), out var d) ? d : fallback;

        return new ReviewSettings(
            D(MinConfidenceKey, "InvoiceReview:MinAutoApproveConfidence", 0.85),
            M(AmountKey, "InvoiceReview:ReviewAmountAbove", 3000m),
            I(DailyKey, "InvoiceReview:MaxDailyUploadsPerCustomer", 10),
            B(KnownStoreKey, "InvoiceReview:RequireKnownStore", true));
    }

    public static async Task SaveAsync(AppDbContext db, ReviewSettings s, string updatedBy)
    {
        var values = new Dictionary<string, string>
        {
            [MinConfidenceKey] = s.MinConfidence.ToString(CultureInfo.InvariantCulture),
            [AmountKey] = s.ReviewAmountAbove.ToString(CultureInfo.InvariantCulture),
            [DailyKey] = s.MaxDailyUploads.ToString(CultureInfo.InvariantCulture),
            [KnownStoreKey] = s.RequireKnownStore.ToString(),
        };
        foreach (var (key, value) in values)
        {
            var row = await db.SystemSettings.FindAsync(key);
            if (row == null) db.SystemSettings.Add(row = new SystemSetting { Key = key });
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = updatedBy;
        }
        await db.SaveChangesAsync();
    }
}

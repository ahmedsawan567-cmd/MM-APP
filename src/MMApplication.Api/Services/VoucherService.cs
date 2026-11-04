using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Services;

public record VoucherInfo(
    string Code, string RewardTitle, decimal DiscountValue, string? MerchantName, string CustomerName, string CustomerPhone,
    string Status, DateTime RedeemedAt, DateTime ExpiresAt, DateTime? UsedAt, string? UsedBy, int PointsSpent);

public record VoucherResult(bool Success, string Message, VoucherInfo? Voucher, int StatusCode = 200);

/// <summary>التحقق من قسائم الخصم واستخدامها عند الكاشير (تطبيق الكاشير ولوحة التحكم).</summary>
public class VoucherService
{
    private readonly AppDbContext _db;

    public VoucherService(AppDbContext db) => _db = db;

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public async Task<VoucherResult> LookupAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return new(false, "كود القسيمة مطلوب.", null, 400);
        var r = await Find(code);
        if (r == null) return new(false, $"القسيمة بالكود '{Normalize(code)}' غير موجودة.", null, 404);
        await ExpireIfNeeded(r);
        return new(true, StatusMessage(r), ToInfo(r));
    }

    public async Task<VoucherResult> UseAsync(string code, string usedBy, string performedBy)
    {
        if (string.IsNullOrWhiteSpace(code)) return new(false, "كود القسيمة مطلوب.", null, 400);
        var r = await Find(code);
        if (r == null) return new(false, $"القسيمة بالكود '{Normalize(code)}' غير موجودة.", null, 404);
        await ExpireIfNeeded(r);

        if (r.Status != RedemptionStatus.ACTIVE) return new(false, StatusMessage(r), ToInfo(r), 400);

        r.Status = RedemptionStatus.USED;
        r.UsedAt = DateTime.UtcNow;
        r.UsedByMerchant = usedBy;
        _db.AuditLogs.Add(new AuditLog
        {
            PerformedBy = performedBy,
            Action = "VOUCHER_USE",
            EntityName = "RewardRedemption",
            EntityId = r.Id.ToString(),
            Details = $"استخدام القسيمة {r.RedemptionCode} ({r.Reward?.Title}) للعميل {r.Customer?.FullName} لدى {usedBy}",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return new(true, "تم التحقق من القسيمة وتطبيق الخصم بنجاح.", ToInfo(r));
    }

    private Task<RewardRedemption?> Find(string code)
    {
        var normalized = Normalize(code);
        return _db.Redemptions.Include(r => r.Reward).Include(r => r.Customer).FirstOrDefaultAsync(r => r.RedemptionCode == normalized);
    }

    private async Task ExpireIfNeeded(RewardRedemption r)
    {
        if (r.Status == RedemptionStatus.ACTIVE && DateTime.UtcNow > r.ExpiresAt)
        {
            r.Status = RedemptionStatus.EXPIRED;
            await _db.SaveChangesAsync();
        }
    }

    private static string StatusMessage(RewardRedemption r) => r.Status switch
    {
        RedemptionStatus.ACTIVE => "القسيمة صالحة ويمكن استخدامها.",
        RedemptionStatus.USED => $"تم استخدام هذه القسيمة مسبقاً بتاريخ {r.UsedAt:yyyy-MM-dd HH:mm} لدى: {r.UsedByMerchant ?? "المتجر"}.",
        RedemptionStatus.EXPIRED => $"هذه القسيمة منتهية الصلاحية منذ {r.ExpiresAt:yyyy-MM-dd}.",
        _ => "هذه القسيمة ملغاة.",
    };

    private static VoucherInfo ToInfo(RewardRedemption r) => new(
        r.RedemptionCode, r.Reward?.Title ?? "-", r.Reward?.DiscountValue ?? 0, r.Reward?.MerchantName,
        r.Customer?.FullName ?? r.CustomerId, r.Customer?.PhoneNumber ?? "", r.Status.ToString(),
        r.RedeemedAt, r.ExpiresAt, r.UsedAt, r.UsedByMerchant, r.PointsSpent);
}

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace MMApplication.Infrastructure.Data;

/// <summary>
/// كود المتجر المطبوع في الباركود (QR) ومكتوب في بطاقات NFC.
/// المحتوى: MMALL-STORE:XXXXXXXXXX — التطبيق يقرأه ويرسل طلب نقاط يؤكده كاشير المتجر.
/// </summary>
public static class StoreCodes
{
    public const string Prefix = "MMALL-STORE:";
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // بدون حروف ملتبسة

    public static string NewCode() => new(RandomNumberGenerator.GetItems<char>(Alphabet, 10));

    public static string Payload(string code) => Prefix + code;

    /// <summary>يستخرج الكود من محتوى الباركود/البطاقة (يقبل الصيغة الكاملة أو الكود وحده أو رابطاً ينتهي به).</summary>
    public static string? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) s = s[Prefix.Length..];
        else if (s.Contains("/s/")) s = s[(s.LastIndexOf("/s/", StringComparison.Ordinal) + 3)..];
        s = s.Trim().ToUpperInvariant();
        return s.Length is >= 6 and <= 20 && s.All(c => char.IsAsciiLetterOrDigit(c)) ? s : null;
    }

    /// <summary>يعطي كل متجر بدون كود كوداً جديداً (عند تشغيل الخادم).</summary>
    public static async Task<int> EnsureCodesAsync(AppDbContext db)
    {
        var missing = await db.Tenants.Where(t => t.StoreCode == null || t.StoreCode == "").ToListAsync();
        foreach (var t in missing) t.StoreCode = NewCode();
        if (missing.Count > 0) await db.SaveChangesAsync();
        return missing.Count;
    }
}

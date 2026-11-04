using System.Text;
using System.Text.RegularExpressions;

namespace MMApplication.Application.Common;

/// <summary>أدوات رقم الفاتورة: الصيغة الموحّدة لكشف التكرار، ونمط أرقام فواتير المتجر.</summary>
public static class InvoiceNumbers
{
    /// <summary>
    /// الصيغة الموحّدة: أرقام لاتينية، أحرف كبيرة، بدون مسافات أو رموز، وبدون أصفار بادئة للأرقام الصِرفة.
    /// "INV-00123" و "inv 00123" و "INV00123" تعطي نفس المفتاح.
    /// </summary>
    public static string? Key(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        var sb = new StringBuilder();
        foreach (var raw in number.Trim())
        {
            var ch = raw switch
            {
                >= '٠' and <= '٩' => (char)('0' + (raw - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (raw - '۰')),
                _ => raw
            };
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
        }
        var key = sb.ToString();
        if (key.Length > 0 && key.All(char.IsDigit)) key = key.TrimStart('0') is { Length: > 0 } t ? t : "0";
        return key.Length == 0 ? null : key[..Math.Min(key.Length, 100)];
    }

    /// <summary>
    /// يولّد نمطاً من مثال حقيقي: الأحرف والرموز كما هي، وكل مجموعة أرقام بطول قريب منها
    /// (لأن العدّاد يكبر مع الوقت). مثال: "INV-2024-000123" ← ^INV-\d{3,6}-\d{5,8}$
    /// </summary>
    public static string? PatternFromExample(string? example)
    {
        if (string.IsNullOrWhiteSpace(example)) return null;
        var text = Normalize(example.Trim());
        var sb = new StringBuilder("^");
        var i = 0;
        while (i < text.Length)
        {
            if (char.IsDigit(text[i]))
            {
                var start = i;
                while (i < text.Length && char.IsDigit(text[i])) i++;
                var len = i - start;
                sb.Append(@"\d{").Append(Math.Max(1, len - 1)).Append(',').Append(len + 2).Append('}');
            }
            else if (char.IsWhiteSpace(text[i]))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                sb.Append(@"\s*");
            }
            else
            {
                sb.Append(Regex.Escape(text[i].ToString()));
                i++;
            }
        }
        return sb.Append('$').ToString();
    }

    /// <summary>هل يطابق الرقم نمط المتجر؟ (null = لا يوجد نمط صالح للمقارنة)</summary>
    public static bool? MatchesPattern(string? number, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(pattern)) return null;
        try
        {
            return Regex.IsMatch(Normalize(number.Trim()), pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        }
        catch (ArgumentException)
        {
            return null; // نمط غير صالح أدخله المستخدم
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    public static bool IsValidPattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        try { _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)); return true; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>يقارن رقمين ضريبيين/هاتفين بعد إزالة المسافات والرموز.</summary>
    public static bool SameIdentifier(string? a, string? b)
    {
        var ka = Key(a);
        var kb = Key(b);
        return ka != null && kb != null && ka == kb;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var raw in s)
        {
            sb.Append(raw switch
            {
                >= '٠' and <= '٩' => (char)('0' + (raw - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (raw - '۰')),
                _ => raw
            });
        }
        return sb.ToString();
    }
}

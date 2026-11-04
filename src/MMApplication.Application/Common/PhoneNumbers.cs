using System.Text;

namespace MMApplication.Application.Common;

/// <summary>
/// توحيد صيغة أرقام الهواتف حتى لا يُفتح حسابان لنفس الرقم بكتابتين مختلفتين
/// (0912345678 و +218912345678 و 00218 91 234 5678 كلها رقم واحد).
/// الصيغة المعتمدة للأرقام الليبية: 09XXXXXXXX. الأرقام الدولية الأخرى: +XXXXXXXX.
/// </summary>
public static class PhoneNumbers
{
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var sb = new StringBuilder();
        var hasPlus = false;
        foreach (var raw in input.Trim())
        {
            var ch = raw switch
            {
                >= '٠' and <= '٩' => (char)('0' + (raw - '٠')), // أرقام عربية ٠-٩
                >= '۰' and <= '۹' => (char)('0' + (raw - '۰')), // أرقام فارسية
                _ => raw
            };
            if (char.IsDigit(ch)) sb.Append(ch);
            else if (ch == '+' && sb.Length == 0) hasPlus = true;
            else if (ch is ' ' or '-' or '(' or ')' or '.' or '‏' or '‎') continue;
            else return null;
        }

        var digits = sb.ToString();
        if (digits.StartsWith("00")) { digits = digits[2..]; hasPlus = true; }

        // ليبيا: 218 9X XXXXXXX
        if (digits.StartsWith("218") && digits.Length == 12) digits = "0" + digits[3..];
        else if (!hasPlus && digits.Length == 9 && digits[0] == '9') digits = "0" + digits;

        if (digits.Length == 10 && digits.StartsWith("09")) return digits;

        // رقم دولي من خارج ليبيا
        if (hasPlus && digits.Length is >= 8 and <= 15 && !digits.StartsWith("218")) return "+" + digits;

        return null;
    }

    public const string InvalidMessage = "رقم الهاتف غير صالح. اكتبه بالصيغة 09XXXXXXXX";
}

using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MMApplication.Infrastructure.Data;

/// <summary>
/// يحدد قاعدة البيانات المستخدمة:
/// - "DatabaseProvider": "Supabase" أو "PostgreSQL" + رابط اتصال حقيقي ⇒ PostgreSQL (Supabase).
/// - غير ذلك ⇒ قاعدة مؤقتة في الذاكرة (للتطوير والاختبارات فقط؛ تُمسح عند إعادة التشغيل).
///
/// رابط الاتصال يُقرأ من ConnectionStrings:SupabaseConnection ثم DefaultConnection،
/// ويُقبل بصيغة Supabase (postgresql://user:pass@host:port/db) أو بصيغة .NET (Host=...;Password=...).
/// </summary>
public static class DatabaseConnection
{
    public record Choice(bool UsePostgres, string? ConnectionString, string Description);

    public static Choice Resolve(IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "InMemory";
        if (!provider.Equals("Supabase", StringComparison.OrdinalIgnoreCase) &&
            !provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            return new(false, null, $"DatabaseProvider = {provider}");
        }

        var raw = configuration.GetConnectionString("SupabaseConnection");
        if (!IsUsable(raw)) raw = configuration.GetConnectionString("DefaultConnection");
        if (!IsUsable(raw))
        {
            return new(false, null, "لم يُضبط رابط اتصال قاعدة البيانات (ConnectionStrings:SupabaseConnection)");
        }

        var connectionString = Normalize(raw!);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return new(true, connectionString, $"PostgreSQL {builder.Host}:{builder.Port}/{builder.Database}");
    }

    private static bool IsUsable(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("SET_VIA_", StringComparison.Ordinal) &&
        // رابط SQL Server القديم في appsettings.Development.json ليس لـ PostgreSQL
        !value.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase) &&
        (value.Contains("Host=", StringComparison.OrdinalIgnoreCase) || value.StartsWith("postgres", StringComparison.OrdinalIgnoreCase));

    /// <summary>يحوّل رابط Supabase (postgresql://...) إلى صيغة Npgsql ويضمن تشفير الاتصال.</summary>
    public static string Normalize(string value)
    {
        NpgsqlConnectionStringBuilder builder;
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(value);
            var userInfo = uri.UserInfo.Split(':', 2);
            builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.Trim('/') is { Length: > 0 } db ? Uri.UnescapeDataString(db) : "postgres",
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            };
        }
        else
        {
            builder = new NpgsqlConnectionStringBuilder(value);
        }

        var isLocal = builder.Host is "localhost" or "127.0.0.1" or "::1";
        if (!isLocal && builder.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
        {
            builder.SslMode = SslMode.Require;
        }

        // منفذ 6543 = Transaction pooler في Supabase: لا يدعم الأوامر المحضّرة مسبقاً
        if (builder.Port == 6543)
        {
            builder.MaxAutoPrepare = 0;
            builder.NoResetOnClose = true;
        }

        builder.ApplicationName ??= "MMApplication";
        return builder.ConnectionString;
    }
}

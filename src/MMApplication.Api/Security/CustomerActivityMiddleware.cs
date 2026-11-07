using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Security;

/// <summary>
/// يسجّل "آخر مرة فتح فيها العميل التطبيق" مع إصدار التطبيق ونوع الجهاز.
/// يُحدَّث مرة كل 5 دقائق على الأكثر لكل عميل حتى لا يُثقل قاعدة البيانات.
/// </summary>
public class CustomerActivityMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<string, DateTime> LastWrite = new();
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        await next(context);

        var user = context.User;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("CUSTOMER") || context.Response.StatusCode >= 400) return;
        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(id)) return;

        var now = DateTime.UtcNow;
        if (LastWrite.TryGetValue(id, out var last) && now - last < Interval) return;
        LastWrite[id] = now;

        var version = Trim(context.Request.Headers["X-App-Version"].ToString(), 30);
        var platform = Trim(context.Request.Headers["X-Platform"].ToString(), 30);
        try
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == id);
            if (customer == null) return;
            customer.LastSeenAt = now;
            if (version != null) customer.LastAppVersion = version;
            if (platform != null) customer.LastPlatform = platform;
            await db.SaveChangesAsync();
        }
        catch (Exception)
        {
            // تسجيل النشاط لا يجب أن يُفشل الطلب
        }
    }

    private static string? Trim(string v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Length <= max ? v : v[..max];
}

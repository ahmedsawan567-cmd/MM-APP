using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Security;

namespace MMApplication.Api.Security;

/// <summary>صلاحيات المستخدم الحالي، تُحمَّل مرة واحدة لكل طلب.</summary>
public static class PermissionContext
{
    private const string ItemKey = "__mm_permissions";

    public static async Task<HashSet<string>> GetAsync(HttpContext http, AppDbContext? db = null)
    {
        if (http.Items.TryGetValue(ItemKey, out var cached) && cached is HashSet<string> set) return set;
        var role = Permissions.RoleOf(http.User);
        db ??= http.RequestServices?.GetService<AppDbContext>();
        var perms = role == null || db == null ? new HashSet<string>() : await Permissions.ForRoleAsync(db, role.Value);
        http.Items[ItemKey] = perms;
        return perms;
    }

    public static async Task<bool> HasAnyAsync(HttpContext http, params string[] permissions) => await HasAnyAsync(http, null, permissions);

    public static async Task<bool> HasAnyAsync(HttpContext http, AppDbContext? db, params string[] permissions)
    {
        var perms = await GetAsync(http, db);
        return permissions.Any(perms.Contains);
    }
}

/// <summary>
/// يمنع الوصول ما لم يملك دور المستخدم إحدى الصلاحيات المذكورة.
/// صفحات المنصة تُعاد للرئيسية، وطلبات JSON/API تُرفض برسالة واضحة.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(params string[] anyOf) : Attribute, IAsyncActionFilter
{
    public string[] AnyOf { get; } = anyOf;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true || await PermissionContext.HasAnyAsync(http, AnyOf))
        {
            await next();
            return;
        }

        var isPage = HttpMethods.IsGet(http.Request.Method) && context.Controller is Controller &&
                     !http.Request.Headers.Accept.ToString().Contains("application/json");
        context.Result = isPage
            ? new RedirectToActionResult("Index", "AdminDashboard", new { denied = 1 })
            : new ObjectResult(new { success = false, message = "ليست لديك صلاحية لهذه العملية. راجع المدير العام." }) { StatusCode = StatusCodes.Status403Forbidden };
    }
}

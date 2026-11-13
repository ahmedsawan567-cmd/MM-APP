using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Infrastructure.Security;

/// <summary>
/// كتالوج صلاحيات المنظومة. كل صفحة وعملية مرتبطة بصلاحية من هنا،
/// والمدير العام يحدد من صفحة "الأدوار والصلاحيات" أي دور يملك أي صلاحية.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    public const string InvoicesView = "invoices.view";
    public const string InvoicesReview = "invoices.review";
    public const string InvoicesEdit = "invoices.edit";
    public const string InvoicesDelete = "invoices.delete";
    public const string FraudView = "fraud.view";
    public const string InvoiceTemplates = "invoice_templates.manage";

    public const string CustomersView = "customers.view";
    public const string CustomersEdit = "customers.edit";
    public const string CustomersPoints = "customers.points";
    public const string CustomersDelete = "customers.delete";
    public const string CustomersExport = "customers.export";

    public const string RewardsManage = "rewards.manage";
    public const string VouchersLog = "vouchers.log";
    public const string VouchersLookup = "vouchers.lookup";
    public const string VouchersRedeem = "vouchers.redeem";
    public const string ClaimsConfirm = "claims.confirm";
    public const string StoreCodes = "stores.codes";

    public const string TiersManage = "tiers.manage";
    public const string LoyaltyManage = "loyalty.manage";
    public const string CampaignsManage = "campaigns.manage";
    public const string MarketingManage = "marketing.manage";

    public const string StoresView = "stores.view";
    public const string StoresManage = "stores.manage";
    public const string SettlementsView = "settlements.view";
    public const string SettlementsManage = "settlements.manage";

    public const string StaffManage = "staff.manage";
    public const string SettingsManage = "settings.manage";
    public const string AuditView = "audit.view";

    public record Definition(string Key, string Group, string Label, string Hint);

    /// <summary>الصلاحيات بالترتيب والتسميات التي تظهر للمدير العام.</summary>
    public static readonly IReadOnlyList<Definition> All =
    [
        new(DashboardView, "الرئيسية والتقارير", "عرض لوحة المؤشرات", "الصفحة الرئيسية بأرقام المبيعات والفواتير"),
        new(ReportsView, "الرئيسية والتقارير", "عرض التقارير", "تقارير المبيعات والنقاط حسب الفترة والمتجر"),
        new(ReportsExport, "الرئيسية والتقارير", "تصدير وطباعة التقارير", "Excel وPDF والطباعة"),

        new(InvoicesView, "الفواتير", "عرض سجل الفواتير", "كل الفواتير وتفاصيلها وصورها"),
        new(InvoicesReview, "الفواتير", "مراجعة الفواتير", "اعتماد الفاتورة أو رفضها أو طلب إعادة تصويرها"),
        new(InvoicesEdit, "الفواتير", "تعديل الفواتير وإلغاؤها", "تصحيح بيانات فاتورة، إدخال فاتورة يدوياً، إلغاء فاتورة معتمدة"),
        new(InvoicesDelete, "الفواتير", "حذف الفواتير نهائياً", "عملية حساسة: تحذف الفاتورة من السجل"),
        new(FraudView, "الفواتير", "الفواتير المشبوهة", "قائمة الفواتير عالية الخطورة والمكررة"),
        new(InvoiceTemplates, "الفواتير", "نماذج فواتير المتاجر", "تعليم النظام شكل فاتورة كل متجر"),

        new(CustomersView, "العملاء", "عرض العملاء وملفاتهم", "بيانات العميل ونقاطه وفواتيره ونشاطه"),
        new(CustomersEdit, "العملاء", "إدارة حسابات العملاء", "إضافة وتعديل عميل، تجميد الحساب، كلمة مرور جديدة، إرسال إشعار"),
        new(CustomersPoints, "العملاء", "إضافة وخصم النقاط يدوياً", "تعديل رصيد العميل مع ذكر السبب"),
        new(CustomersDelete, "العملاء", "حذف العملاء", "يغلق الحساب ويمسح البيانات الشخصية"),
        new(CustomersExport, "العملاء", "تصدير وطباعة بيانات العملاء", "قوائم وتقارير العملاء Excel وPDF"),

        new(RewardsManage, "المكافآت والعضويات", "إدارة المكافآت", "إضافة وتعديل وإيقاف وحذف المكافآت"),
        new(TiersManage, "المكافآت والعضويات", "إدارة العضويات", "مستويات العضوية وشروطها ومزاياها"),
        new(VouchersLog, "المكافآت والعضويات", "سجل القسائم", "كل القسائم التي استبدلها العملاء"),
        new(VouchersLookup, "المكافآت والعضويات", "التحقق من قسيمة", "البحث عن قسيمة بالكود ومعرفة حالتها"),
        new(VouchersRedeem, "المكافآت والعضويات", "تأكيد استخدام القسيمة", "صرف القسيمة للعميل عند الدفع"),
        new(ClaimsConfirm, "الفواتير", "سجل مسح الباركود وNFC", "عرض من مسح باركود المتجر وإلغاء المسح المشبوه"),
        new(StoreCodes, "المتاجر", "باركود المتجر وعملاؤه", "تحديد نقاط الباركود وطباعته وتغيير الكود، وعرض عملاء الباركود والفواتير"),

        new(LoyaltyManage, "النقاط والتسويق", "قواعد النقاط للمتاجر", "كم نقطة تُكسب في كل متجر والعروض المضاعفة"),
        new(CampaignsManage, "النقاط والتسويق", "قواعد المول العامة والحملات", "القاعدة العامة للنقاط وحملات المول"),
        new(MarketingManage, "النقاط والتسويق", "الإعلانات والإشعارات", "بنرات التطبيق والإشعارات الجماعية"),

        new(StoresView, "المتاجر", "عرض المتاجر", "بيانات المتاجر وأسماؤها في الفواتير"),
        new(StoresManage, "المتاجر", "إدارة المتاجر", "إضافة وتعديل وحذف متجر وأسمائه البديلة"),
        new(SettlementsView, "المتاجر", "عرض مستحقات المتاجر", "مبالغ القسائم المستحقة لكل متجر"),
        new(SettlementsManage, "المتاجر", "إدارة المستحقات", "إصدار وسداد وإلغاء دفعات المستحقات"),

        new(StaffManage, "الإدارة", "إدارة المستخدمين والصلاحيات", "حسابات الموظفين وأدوارهم وهذه الصفحة"),
        new(SettingsManage, "الإدارة", "الإعدادات", "شروط قبول الفواتير وحالة النظام"),
        new(AuditView, "الإدارة", "سجل العمليات", "كل عملية نفّذها الموظفون"),
    ];

    public static bool IsKnown(string key) => All.Any(p => p.Key == key);

    /// <summary>الصلاحيات الافتراضية لكل دور (تُطبَّق أول مرة فقط، ثم يعدّلها المدير العام).</summary>
    public static readonly IReadOnlyDictionary<SystemUserRole, string[]> Defaults = new Dictionary<SystemUserRole, string[]>
    {
        [SystemUserRole.FRAUD_AUDITOR] =
        [
            DashboardView, ReportsView, ReportsExport, InvoicesView, InvoicesReview, InvoicesEdit, FraudView, InvoiceTemplates,
            CustomersView, CustomersEdit, CustomersExport, VouchersLog, VouchersLookup, StoresView, SettlementsView, ClaimsConfirm,
        ],
        [SystemUserRole.STORE_MANAGER] =
        [
            DashboardView, ReportsView, ReportsExport, InvoicesView, InvoiceTemplates, LoyaltyManage, StoresView,
            SettlementsView, VouchersLookup, VouchersRedeem, ClaimsConfirm, StoreCodes,
        ],
        [SystemUserRole.CASHIER] = [DashboardView, InvoicesView, VouchersLookup, VouchersRedeem, ClaimsConfirm],
    };

    /// <summary>الأدوار القابلة للتعديل (المدير العام ثابت بكل الصلاحيات حتى لا يُقفل النظام).</summary>
    public static readonly SystemUserRole[] EditableRoles = [SystemUserRole.FRAUD_AUDITOR, SystemUserRole.STORE_MANAGER, SystemUserRole.CASHIER];

    /// <summary>دور المستخدم من الكوكي (SystemRole) أو التوكن (Role).</summary>
    public static SystemUserRole? RoleOf(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        var raw = user.FindFirst("SystemRole")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value;
        return raw?.ToLowerInvariant() switch
        {
            "super_admin" or "superadmin" or "admin" => SystemUserRole.SUPER_ADMIN,
            "fraud_auditor" or "auditor" => SystemUserRole.FRAUD_AUDITOR,
            "store_manager" or "storemanager" => SystemUserRole.STORE_MANAGER,
            "cashier" => SystemUserRole.CASHIER,
            _ => null,
        };
    }

    private const string SeededPrefix = "Permissions.Seeded.";

    /// <summary>عند التشغيل: يضع الصلاحيات الافتراضية للأدوار التي لم تُضبط بعد.</summary>
    public static async Task EnsureDefaultsAsync(AppDbContext db)
    {
        foreach (var (role, perms) in Defaults)
        {
            var key = SeededPrefix + role;
            var seededRow = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (seededRow != null)
            {
                // صلاحيات افتراضية أضيفت للمنظومة بعد التهيئة الأولى: تُمنح مرة واحدة فقط
                // (ولا تُعاد إن سحبها المدير العام لاحقاً)
                var known = seededRow.Value == "1" ? V1Defaults(role) : seededRow.Value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                var fresh = perms.Where(p => !known.Contains(p)).ToList();
                if (fresh.Count > 0)
                {
                    var have = await db.RolePermissions.Where(p => p.Role == role.ToString()).Select(p => p.Permission).ToListAsync();
                    db.RolePermissions.AddRange(fresh.Except(have).Select(p => new RolePermission { Role = role.ToString(), Permission = p }));
                }
                seededRow.Value = string.Join(',', known.Union(perms));
                continue;
            }
            var existing = await db.RolePermissions.Where(p => p.Role == role.ToString()).Select(p => p.Permission).ToListAsync();
            db.RolePermissions.AddRange(perms.Except(existing).Select(p => new RolePermission { Role = role.ToString(), Permission = p }));
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = string.Join(',', perms), UpdatedAt = DateTime.UtcNow, UpdatedBy = "النظام" });
        }
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
        {
            // نسخة أخرى من الخادم سبقتنا للتهيئة على نفس القاعدة
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>الصلاحيات الافتراضية في الإصدار الأول (قبل إضافة صلاحيات الباركود).</summary>
    private static HashSet<string> V1Defaults(SystemUserRole role) =>
        (Defaults.TryGetValue(role, out var d) ? d : []).Except([ClaimsConfirm, StoreCodes]).ToHashSet();

    public static async Task<HashSet<string>> ForRoleAsync(AppDbContext db, SystemUserRole role)
    {
        if (role == SystemUserRole.SUPER_ADMIN) return All.Select(p => p.Key).ToHashSet();
        // قبل تهيئة الجدول (اختبارات أو أول تشغيل) تُستخدم الافتراضية
        var seeded = await db.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == SeededPrefix + role);
        if (!seeded) return Defaults.TryGetValue(role, out var d) ? d.ToHashSet() : [];
        return (await db.RolePermissions.AsNoTracking().Where(p => p.Role == role.ToString()).Select(p => p.Permission).ToListAsync()).ToHashSet();
    }

    public static async Task SaveRoleAsync(AppDbContext db, SystemUserRole role, IEnumerable<string> permissions)
    {
        if (role == SystemUserRole.SUPER_ADMIN) throw new InvalidOperationException("صلاحيات المدير العام ثابتة.");
        var wanted = permissions.Where(IsKnown).Distinct().ToHashSet();
        var current = await db.RolePermissions.Where(p => p.Role == role.ToString()).ToListAsync();
        db.RolePermissions.RemoveRange(current.Where(p => !wanted.Contains(p.Permission)));
        db.RolePermissions.AddRange(wanted.Except(current.Select(c => c.Permission)).Select(p => new RolePermission { Role = role.ToString(), Permission = p }));
        var key = SeededPrefix + role;
        if (await db.SystemSettings.FindAsync(key) == null)
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = string.Join(',', Defaults.TryGetValue(role, out var dd) ? dd : []), UpdatedAt = DateTime.UtcNow, UpdatedBy = "النظام" });
    }
}

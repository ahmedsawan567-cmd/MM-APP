namespace MMApplication.Api.Controllers;

/// <summary>
/// أسماء أدوار طاقم العمل كما تظهر في الكوكي (لوحة التحكم) وفي توكن JWT (تسجيل دخول الموظفين عبر الـ API).
/// العملاء (CUSTOMER) غير مشمولين في أي منها.
/// </summary>
public static class StaffRoles
{
    /// <summary>كل طاقم العمل بما فيهم الكاشير (للدخول إلى لوحة التحكم).</summary>
    public const string All =
        "superadmin,auditor,storemanager,cashier," +
        "SUPER_ADMIN,FRAUD_AUDITOR,STORE_MANAGER,CASHIER,Admin";

    /// <summary>الإدارة فقط (بدون الكاشير) لواجهات الإدارة الحساسة.</summary>
    public const string Management =
        "superadmin,auditor,storemanager," +
        "SUPER_ADMIN,FRAUD_AUDITOR,STORE_MANAGER,Admin";
}

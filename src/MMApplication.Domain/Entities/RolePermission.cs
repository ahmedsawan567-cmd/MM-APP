namespace MMApplication.Domain.Entities;

/// <summary>صلاحية ممنوحة لدور وظيفي (يعدّلها المدير العام من صفحة الأدوار والصلاحيات).</summary>
public class RolePermission
{
    /// <summary>الدور: FRAUD_AUDITOR أو STORE_MANAGER أو CASHIER (المدير العام يملك كل الصلاحيات دائماً).</summary>
    public string Role { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}

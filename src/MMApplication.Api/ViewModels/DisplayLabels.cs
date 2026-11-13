using MMApplication.Domain.Enums;

namespace MMApplication.Api.ViewModels;

/// <summary>تسميات عربية موحّدة لعرض القيم الداخلية في المنصة.</summary>
public static class DisplayLabels
{
    public static string Tier(string? tier) => MMApplication.Infrastructure.Data.MembershipTiers.NameOf(tier);

    /// <summary>لون العضوية المخصص (يُستخدم كنمط مباشر على الشارة).</summary>
    public static string TierStyle(string? tier) =>
        MMApplication.Infrastructure.Data.MembershipTiers.ColorOf(tier) is { } c ? $"background-color:{c}1a;color:{c};--tw-ring-color:{c}40" : "";

    public static string TierBadge(string? tier) => (tier ?? "").ToUpperInvariant() switch
    {
        "VIP" => "badge-purple",
        "GOLD" => "badge-amber",
        "SILVER" => "badge-slate",
        _ => "badge-orange",
    };

    public static string LedgerType(WalletTransactionType t) => t switch
    {
        WalletTransactionType.EARN_RECEIPT => "نقاط فاتورة",
        WalletTransactionType.REDEEM_PURCHASE => "استبدال مكافأة",
        WalletTransactionType.POINTS_EXPIRED => "انتهاء صلاحية",
        WalletTransactionType.MANUAL_ADJUSTMENT => "تعديل يدوي",
        _ => t.ToString(),
    };

    public static (string Label, string Badge) Redemption(RedemptionStatus s, DateTime expiresAt) =>
        s == RedemptionStatus.ACTIVE && expiresAt < DateTime.UtcNow ? ("منتهية", "badge-slate") : s switch
        {
            RedemptionStatus.ACTIVE => ("صالحة", "badge-emerald"),
            RedemptionStatus.USED => ("مستخدمة", "badge-brand"),
            RedemptionStatus.EXPIRED => ("منتهية", "badge-slate"),
            RedemptionStatus.CANCELLED => ("ملغاة", "badge-rose"),
            _ => (s.ToString(), "badge-slate"),
        };

    public static (string Label, string Badge) Invoice(InvoiceStatus s) => s switch
    {
        InvoiceStatus.APPROVED => ("معتمدة", "badge-emerald"),
        InvoiceStatus.REJECTED => ("مرفوضة", "badge-rose"),
        InvoiceStatus.VOIDED => ("ملغاة", "badge-slate"),
        InvoiceStatus.REVIEW_REQUIRED => ("قيد المراجعة", "badge-amber"),
        InvoiceStatus.PENDING => ("قيد المعالجة", "badge-amber"),
        InvoiceStatus.AWAITING_STORE => ("بانتظار تأكيد المتجر", "badge-amber"),
        _ => (s.ToString(), "badge-slate"),
    };

    public static string Role(SystemUserRole r) => r switch
    {
        SystemUserRole.SUPER_ADMIN => "المدير العام",
        SystemUserRole.FRAUD_AUDITOR => "مدقق الفواتير",
        SystemUserRole.STORE_MANAGER => "مدير متجر",
        SystemUserRole.CASHIER => "كاشير",
        _ => r.ToString(),
    };

    private static readonly Dictionary<string, string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ADMIN_PASSWORD_RESET"] = "إعادة ضبط كلمة مرور المدير",
        ["BANNER_CREATE"] = "إنشاء بنر",
        ["BANNER_UPDATE"] = "تعديل بنر",
        ["CAMPAIGN_CREATE"] = "إنشاء حملة",
        ["CAMPAIGN_UPDATE"] = "تعديل حملة",
        ["CUSTOMER_ARCHIVE"] = "أرشفة عميل",
        ["CUSTOMER_CREATE"] = "إنشاء عميل",
        ["CUSTOMER_FREEZE"] = "تجميد عميل",
        ["CUSTOMER_SUSPEND"] = "تجميد عميل",
        ["CUSTOMER_UNSUSPEND"] = "تنشيط عميل",
        ["CUSTOMER_PASSWORD_RESET"] = "كلمة مرور جديدة لعميل",
        ["CUSTOMER_POINTS_ADJUST"] = "تعديل نقاط",
        ["CUSTOMER_SELF_DELETE"] = "حذف العميل لحسابه",
        ["CUSTOMER_UPDATE"] = "تعديل بيانات عميل",
        ["CUSTOMER_DELETE"] = "حذف عميل",
        ["CUSTOMER_ANONYMIZE"] = "إخفاء هوية عميل محذوف",
        ["CUSTOMER_NOTIFY"] = "إشعار لعميل",
        ["FRAUD_AUDIT_ACTION"] = "إجراء تدقيق",
        ["FRAUD_CONFIRM"] = "تأكيد احتيال",
        ["FRAUD_QUEUE_APPROVE"] = "قبول من التدقيق",
        ["FRAUD_QUEUE_REJECT"] = "رفض من التدقيق",
        ["DISPUTE_ACCEPT"] = "قبول اعتراض",
        ["ACCEPT_DISPUTE"] = "قبول اعتراض",
        ["DISPUTE_REJECT"] = "رفض اعتراض",
        ["REJECT_DISPUTE"] = "رفض اعتراض",
        ["INVOICE_AI_REREAD"] = "إعادة قراءة فاتورة",
        ["INVOICE_APPROVE"] = "اعتماد فاتورة",
        ["INVOICE_APPROVED"] = "اعتماد فاتورة",
        ["INVOICE_REJECT"] = "رفض فاتورة",
        ["INVOICE_REJECTED"] = "رفض فاتورة",
        ["INVOICE_VOID"] = "إلغاء فاتورة",
        ["INVOICE_VOIDED"] = "إلغاء فاتورة",
        ["INVOICE_DISPUTE"] = "اعتراض على فاتورة",
        ["INVOICE_DUPLICATE"] = "محاولة فاتورة مكررة",
        ["INVOICE_DUPLICATE_SELF"] = "تكرار فاتورة (نفس العميل)",
        ["INVOICE_DUPLICATE_OTHER_ACCOUNT"] = "فاتورة مستخدمة من حساب آخر",
        ["INVOICE_MANUAL_CREATE"] = "إدخال فاتورة يدوياً",
        ["INVOICE_PROFILE_DELETE"] = "حذف نموذج فاتورة",
        ["INVOICE_PROFILE_SAVE"] = "حفظ نموذج فاتورة",
        ["INVOICE_REQUEST_RESCAN"] = "طلب إعادة تصوير",
        ["INVOICE_RESUBMIT"] = "إعادة رفع فاتورة",
        ["INVOICE_UPDATE"] = "تعديل فاتورة",
        ["UPDATE_INVOICE_STATUS"] = "تغيير حالة فاتورة",
        ["LOYALTY_EARNING_RULE_CREATE"] = "إنشاء قاعدة نقاط",
        ["LOYALTY_EARNING_RULE_UPDATE"] = "تعديل قاعدة نقاط",
        ["LOYALTY_RULES_UPDATE"] = "تعديل قواعد الولاء",
        ["NOTIFICATION_BROADCAST"] = "إرسال إشعار جماعي",
        ["NOTIFICATION_RETRACT"] = "سحب إشعار",
        ["PRODUCT_BOOSTER_CREATE"] = "إنشاء عرض مضاعف",
        ["PRODUCT_BOOSTER_UPDATE"] = "تعديل عرض مضاعف",
        ["REPORT_EXPORT"] = "تصدير تقرير",
        ["REWARD_CREATE"] = "إنشاء مكافأة",
        ["REWARD_UPDATE"] = "تعديل مكافأة",
        ["REWARD_DELETE"] = "حذف مكافأة",
        ["REWARD_DEACTIVATE"] = "إيقاف مكافأة",
        ["REWARD_TOGGLE"] = "إيقاف/تفعيل مكافأة",
        ["SETTINGS_UPDATE"] = "تعديل الإعدادات",
        ["SETTLEMENT_BATCH_GENERATE"] = "إصدار تسويات",
        ["SETTLEMENT_CANCEL"] = "إلغاء تسوية",
        ["SETTLEMENT_SETTLE"] = "سداد تسوية",
        ["STAFF_CREATE"] = "إضافة موظف",
        ["STAFF_DELETE"] = "حذف موظف",
        ["STAFF_UPDATE"] = "تعديل موظف",
        ["STAFF_PASSWORD_RESET"] = "كلمة مرور جديدة لموظف",
        ["STAFF_LOGIN"] = "دخول موظف",
        ["STAFF_LOGIN_FAILED"] = "محاولة دخول فاشلة",
        ["STAFF_LOCKED"] = "قفل حساب موظف مؤقتاً",
        ["STORE_CODE_REGENERATE"] = "تغيير باركود متجر",
        ["STORE_SCAN_SETTINGS"] = "تعديل نقاط باركود متجر",
        ["ROLE_PERMISSIONS_UPDATE"] = "تعديل صلاحيات دور",
        ["TIER_CREATE"] = "إضافة عضوية",
        ["TIER_UPDATE"] = "تعديل عضوية",
        ["TIER_DELETE"] = "حذف عضوية",
        ["CUSTOMER_TIER_CHANGE"] = "تغيير عضوية عميل",
        ["CUSTOMER_PASSWORD_CHANGE"] = "تغيير العميل لكلمة مروره",
        ["STAFF_ACTIVATE"] = "تفعيل حساب موظف",
        ["STAFF_DEACTIVATE"] = "تعطيل حساب موظف",
        ["STORE_ALIAS_CREATE"] = "إضافة اسم بديل لمتجر",
        ["STORE_CREATE"] = "إضافة متجر",
        ["STORE_UPDATE"] = "تعديل متجر",
        ["STORE_DELETE"] = "حذف متجر",
        ["SYSTEM_INITIALIZATION"] = "تهيئة النظام",
        ["VOUCHER_USE"] = "استخدام قسيمة",
    };

    public static string Action(string? action) =>
        action != null && Actions.TryGetValue(action, out var label) ? label : (action ?? "");

    public static IEnumerable<KeyValuePair<string, string>> KnownActions => Actions.GroupBy(a => a.Value).Select(g => g.First()).OrderBy(a => a.Value);

    /// <summary>لون شارة العملية حسب نوعها.</summary>
    public static string ActionBadge(string? action)
    {
        var a = (action ?? "").ToUpperInvariant();
        if (a.Contains("DELETE") || a.Contains("REJECT") || a.Contains("VOID") || a.Contains("FRAUD") || a.Contains("FREEZE") || a.Contains("SUSPEND") || a.Contains("FAILED") || a.Contains("LOCKED") || a.Contains("DEACTIVATE")) return "badge-rose";
        if (a.Contains("CREATE") || a.Contains("APPROVE") || a.Contains("SETTLE") || a.Contains("UNSUSPEND")) return "badge-emerald";
        if (a.Contains("PASSWORD") || a.Contains("ADJUST") || a.Contains("SETTINGS") || a.Contains("PERMISSIONS")) return "badge-amber";
        return "badge-slate";
    }
}

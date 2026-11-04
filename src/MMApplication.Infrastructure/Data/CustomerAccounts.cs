using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data;

/// <summary>أرقام العضوية المميزة للعملاء (MM-100001، MM-100002، ...).</summary>
public static class CustomerAccounts
{
    public const string MembershipPrefix = "MM-";
    private const int FirstNumber = 100001;

    public static async Task<string> NextMembershipNumberAsync(AppDbContext db)
    {
        var last = await db.Customers
            .Where(c => c.MembershipNumber != null && c.MembershipNumber.StartsWith(MembershipPrefix))
            .OrderByDescending(c => c.MembershipNumber!.Length)
            .ThenByDescending(c => c.MembershipNumber)
            .Select(c => c.MembershipNumber)
            .FirstOrDefaultAsync();

        var next = last != null && int.TryParse(last[MembershipPrefix.Length..], out var n) ? n + 1 : FirstNumber;
        // يشمل العملاء المضافين في نفس العملية ولم يُحفظوا بعد
        foreach (var pending in db.ChangeTracker.Entries<Customer>().Where(e => e.State == EntityState.Added))
        {
            var m = pending.Entity.MembershipNumber;
            if (m != null && m.StartsWith(MembershipPrefix) && int.TryParse(m[MembershipPrefix.Length..], out var p) && p >= next) next = p + 1;
        }
        return MembershipPrefix + next;
    }

    /// <summary>
    /// يحفظ عميلاً جديداً مع رقم عضوية فريد. عند تزامن تسجيلين يُعاد المحاولة برقم جديد.
    /// </summary>
    public static async Task SaveNewCustomerAsync(AppDbContext db, Customer customer)
    {
        for (var attempt = 0; ; attempt++)
        {
            customer.MembershipNumber = await NextMembershipNumberAsync(db);
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException) when (attempt < 4)
            {
                // تعارض رقم العضوية مع تسجيل متزامن: نعيد المحاولة
            }
        }
    }

    /// <summary>يعطي رقم عضوية لكل عميل قديم بدونه (بترتيب تاريخ التسجيل).</summary>
    public static async Task<int> EnsureMembershipNumbersAsync(AppDbContext db)
    {
        var missing = await db.Customers
            .Where(c => c.MembershipNumber == null || c.MembershipNumber == "")
            .OrderBy(c => c.RegisteredAt)
            .ToListAsync();
        if (missing.Count == 0) return 0;

        var first = await NextMembershipNumberAsync(db);
        var next = int.Parse(first[MembershipPrefix.Length..]);
        foreach (var c in missing) c.MembershipNumber = MembershipPrefix + next++;
        await db.SaveChangesAsync();
        return missing.Count;
    }

    /// <summary>
    /// إغلاق الحساب نهائياً مع مسح البيانات الشخصية. رقم الهاتف يتحرر فيمكن لصاحبه التسجيل من جديد،
    /// ورقم العضوية القديم لا يُعاد استخدامه.
    /// </summary>
    public static void Anonymize(Customer customer)
    {
        customer.FullName = "حساب محذوف";
        customer.PhoneNumber = "DEL-" + customer.CustomerId.Replace("CUST-", "")[..Math.Min(12, customer.CustomerId.Replace("CUST-", "").Length)];
        customer.Email = null;
        customer.BirthDate = null;
        customer.Gender = null;
        customer.PasswordHash = null;
        customer.DeviceFingerprint = null;
        customer.IsArchived = true;
        customer.IsFrozen = true;
        customer.UpdatedAt = DateTime.UtcNow;
    }
}

using Microsoft.EntityFrameworkCore;
using MMApplication.Application.DTOs;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Services;

/// <summary>يبني قائمة متاجر المول وأسمائها البديلة لإرسالها لمحرك قراءة الفواتير.</summary>
public static class ReceiptContextBuilder
{
    public static async Task<ReceiptExtractionContext> BuildAsync(AppDbContext db, string? contentType = null, string? fileName = null) => new()
    {
        ContentType = contentType,
        FileName = fileName,
        KnownStores = await db.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new KnownStoreDto
            {
                TenantId = t.TenantId,
                NameAr = t.NameAr,
                NameEn = t.NameEn,
                Aliases = t.Aliases.Where(a => a.IsActive).Select(a => a.AliasName).ToList(),
                TaxNumber = t.InvoiceProfile != null && t.InvoiceProfile.IsActive ? t.InvoiceProfile.TaxNumber : null,
                InvoiceNumberLabel = t.InvoiceProfile != null && t.InvoiceProfile.IsActive ? t.InvoiceProfile.InvoiceNumberLabel : null,
                InvoiceNumberExample = t.InvoiceProfile != null && t.InvoiceProfile.IsActive ? t.InvoiceProfile.InvoiceNumberExample : null,
                LayoutNotes = t.InvoiceProfile != null && t.InvoiceProfile.IsActive ? t.InvoiceProfile.LayoutNotes : null,
            })
            .ToListAsync()
    };
}

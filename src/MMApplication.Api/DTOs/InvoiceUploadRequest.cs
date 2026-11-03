using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MMApplication.Api.DTOs;

public class InvoiceUploadRequest
{
    [Required(ErrorMessage = "صورة الفاتورة مطلوبة (Receipt file is required)")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// معرف العميل (اختياري: إذا لم يتم تمريره، يتم استخراجه تلقائياً من الـ JWT Token الخاص بالمستخدم الحالي).
    /// </summary>
    public string? CustomerId { get; set; }

    /// <summary>
    /// بصمة الجهاز (Device Fingerprint / Hardware UUID) لمنع الحسابات الوهمية (Anti-Sybil).
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// طراز الجهاز (Device Model e.g. iPhone 15, Galaxy S24).
    /// </summary>
    public string? DeviceModel { get; set; }
}

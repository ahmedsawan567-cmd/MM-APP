namespace MMApplication.Domain.Entities;

/// <summary>
/// سجل أجهزة العملاء وبصمات الهواتف لمنع الحسابات الوهمية (Anti-Sybil Engine)
/// </summary>
public class CustomerDevice
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty; // IMEI / MAC Hash / Hardware UUID
    public string? DeviceModel { get; set; }                      // iPhone 15 Pro, Samsung S24, etc.
    public string? OsVersion { get; set; }
    public string? IpAddress { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Customer Customer { get; set; } = null!;
}

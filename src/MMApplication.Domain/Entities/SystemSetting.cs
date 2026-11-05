namespace MMApplication.Domain.Entities;

/// <summary>إعداد عام قابل للتعديل من لوحة التحكم (مفتاح/قيمة).</summary>
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}

namespace Platform.Domain.Entities;

public class PlatformSetting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}

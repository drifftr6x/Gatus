namespace Platform.Domain.Entities;

public class NotificationChannel
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; } // email, webhook, teams
    public string ConfigJson { get; set; } = "{}"; // type-specific config
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

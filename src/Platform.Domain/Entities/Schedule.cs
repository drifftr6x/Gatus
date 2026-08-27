namespace Platform.Domain.Entities;

public class Schedule
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid ContentId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Priority { get; set; } = 0;
    public ScheduleRecurrence Recurrence { get; set; } = ScheduleRecurrence.Once;
    public string? RecurrencePattern { get; set; } // RRULE format
    public bool IsActive { get; set; } = true;
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
    public Content Content { get; set; } = null!;
    public User? CreatedBy { get; set; }
}

public enum ScheduleRecurrence
{
    Once,
    Daily,
    Weekly,
    Monthly,
    Custom
}

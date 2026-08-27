using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("schedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.DeviceId).HasColumnName("device_id");
        builder.Property(s => s.ContentId).HasColumnName("content_id");
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(s => s.StartTime).HasColumnName("start_time");
        builder.Property(s => s.EndTime).HasColumnName("end_time");
        builder.Property(s => s.Priority).HasColumnName("priority");
        builder.Property(s => s.Recurrence).HasColumnName("recurrence").HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.RecurrencePattern).HasColumnName("recurrence_pattern").HasMaxLength(500);
        builder.Property(s => s.IsActive).HasColumnName("is_active");
        builder.Property(s => s.CreatedById).HasColumnName("created_by_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(s => s.Device)
            .WithMany(d => d.Schedules)
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Content)
            .WithMany(c => c.Schedules)
            .HasForeignKey(s => s.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.CreatedBy)
            .WithMany(u => u.CreatedSchedules)
            .HasForeignKey(s => s.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => s.DeviceId);
        builder.HasIndex(s => s.ContentId);
        builder.HasIndex(s => s.StartTime);
        builder.HasIndex(s => s.EndTime);
        builder.HasIndex(s => s.IsActive);
    }
}

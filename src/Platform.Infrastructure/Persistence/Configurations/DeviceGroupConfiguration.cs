using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class DeviceGroupConfiguration : IEntityTypeConfiguration<DeviceGroup>
{
    public void Configure(EntityTypeBuilder<DeviceGroup> builder)
    {
        builder.ToTable("device_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(g => g.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(g => g.MaintenanceWindowStart).HasColumnName("maintenance_window_start");
        builder.Property(g => g.MaintenanceWindowDurationMinutes).HasColumnName("maintenance_window_duration_minutes");
        builder.Property(g => g.MaintenanceWindowDays).HasColumnName("maintenance_window_days").HasMaxLength(50);
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(g => g.Name).IsUnique();
    }
}

public class DeviceConfigTemplateConfiguration : IEntityTypeConfiguration<DeviceConfigTemplate>
{
    public void Configure(EntityTypeBuilder<DeviceConfigTemplate> builder)
    {
        builder.ToTable("device_config_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(t => t.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.Name).IsUnique();
    }
}

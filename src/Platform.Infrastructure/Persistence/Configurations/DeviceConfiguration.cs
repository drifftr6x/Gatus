using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.SerialNumber).HasColumnName("serial_number").HasMaxLength(100);
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(d => d.Location).HasColumnName("location").HasMaxLength(500);
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(d => d.Hostname).HasColumnName("hostname").HasMaxLength(255);
        builder.Property(d => d.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(d => d.MacAddress).HasColumnName("mac_address").HasMaxLength(17);
        builder.Property(d => d.FirmwareVersion).HasColumnName("firmware_version").HasMaxLength(50);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.IsActive).HasColumnName("is_active");
        builder.Property(d => d.GroupId).HasColumnName("group_id");
        builder.Property(d => d.Tags).HasColumnName("tags").HasMaxLength(500);
        builder.Property(d => d.Latitude).HasColumnName("latitude");
        builder.Property(d => d.Longitude).HasColumnName("longitude");

        builder.HasOne(d => d.Group)
            .WithMany(g => g.Devices)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.SerialNumber);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.IsActive);
    }
}

public class DeviceTelemetryConfiguration : IEntityTypeConfiguration<DeviceTelemetry>
{
    public void Configure(EntityTypeBuilder<DeviceTelemetry> builder)
    {
        builder.ToTable("device_telemetry");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.DeviceId).HasColumnName("device_id");
        builder.Property(t => t.Timestamp).HasColumnName("timestamp");
        builder.Property(t => t.MetricName).HasColumnName("metric_name").HasMaxLength(100);
        builder.Property(t => t.MetricValue).HasColumnName("metric_value").HasMaxLength(500);
        builder.Property(t => t.Unit).HasColumnName("unit").HasMaxLength(20);

        builder.HasOne(t => t.Device)
            .WithMany(d => d.Telemetry)
            .HasForeignKey(t => t.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.DeviceId);
        builder.HasIndex(t => t.Timestamp);
    }
}

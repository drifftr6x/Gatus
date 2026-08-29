using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class CommandConfiguration : IEntityTypeConfiguration<Command>
{
    public void Configure(EntityTypeBuilder<Command> builder)
    {
        builder.ToTable("commands");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.DeviceId).HasColumnName("device_id");
        builder.Property(c => c.Type).HasColumnName("type").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Payload).HasColumnName("payload");
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.ExpiresAt).HasColumnName("expires_at");
        builder.Property(c => c.TimeoutSeconds).HasColumnName("timeout_seconds");
        builder.Property(c => c.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(c => c.CompletedAt).HasColumnName("completed_at");
        builder.Property(c => c.ResultMessage).HasColumnName("result_message").HasMaxLength(2000);

        builder.HasOne(c => c.Device)
            .WithMany()
            .HasForeignKey(c => c.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.DeviceId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => new { c.DeviceId, c.Status });
        builder.HasIndex(c => c.CreatedAt);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.ToTable("content_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.ContentId).HasColumnName("content_id");
        builder.Property(v => v.Version).HasColumnName("version");
        builder.Property(v => v.Sha256Checksum).HasColumnName("sha256_checksum").HasMaxLength(64).IsRequired();
        builder.Property(v => v.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(v => v.StoragePath).HasColumnName("storage_path").HasMaxLength(500).IsRequired();
        builder.Property(v => v.MimeType).HasColumnName("mime_type").HasMaxLength(100);
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.CreatedById).HasColumnName("created_by_id");
        builder.Property(v => v.IsActive).HasColumnName("is_active");
        builder.Property(v => v.ReleaseNotes).HasColumnName("release_notes").HasMaxLength(2000);

        builder.HasOne(v => v.Content)
            .WithMany()
            .HasForeignKey(v => v.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.ContentId, v.Version }).IsUnique();
    }
}

public class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.ToTable("deployments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(d => d.ContentVersionId).HasColumnName("content_version_id");
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(d => d.StartedAt).HasColumnName("started_at");
        builder.Property(d => d.CompletedAt).HasColumnName("completed_at");
        builder.Property(d => d.CreatedById).HasColumnName("created_by_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(d => d.ContentVersion)
            .WithMany(v => v.Deployments)
            .HasForeignKey(d => d.ContentVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.Status);
    }
}

public class DeploymentResultConfiguration : IEntityTypeConfiguration<DeploymentResult>
{
    public void Configure(EntityTypeBuilder<DeploymentResult> builder)
    {
        builder.ToTable("deployment_results");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.DeploymentId).HasColumnName("deployment_id");
        builder.Property(r => r.DeviceId).HasColumnName("device_id");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.StartedAt).HasColumnName("started_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(r => r.RollbackPerformed).HasColumnName("rollback_performed");
        builder.Property(r => r.PreviousVersionId).HasColumnName("previous_version_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(r => r.Deployment)
            .WithMany(d => d.Results)
            .HasForeignKey(r => r.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Device)
            .WithMany()
            .HasForeignKey(r => r.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.DeploymentId);
        builder.HasIndex(r => r.DeviceId);
        builder.HasIndex(r => r.Status);
    }
}

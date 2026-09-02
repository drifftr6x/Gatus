using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class AgentUpdateConfiguration : IEntityTypeConfiguration<AgentUpdate>
{
    public void Configure(EntityTypeBuilder<AgentUpdate> builder)
    {
        builder.ToTable("agent_updates");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Version).HasColumnName("version").HasMaxLength(32).IsRequired();
        builder.Property(u => u.Sha256Checksum).HasColumnName("sha256_checksum").HasMaxLength(64).IsRequired();
        builder.Property(u => u.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(u => u.StoragePath).HasColumnName("storage_path").HasMaxLength(500).IsRequired();
        builder.Property(u => u.RolloutPercent).HasColumnName("rollout_percent");
        builder.Property(u => u.MinVersion).HasColumnName("min_version").HasMaxLength(32);
        builder.Property(u => u.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(u => u.IsActive).HasColumnName("is_active");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.CreatedById).HasColumnName("created_by_id");

        builder.HasIndex(u => u.Version).IsUnique();
        builder.HasIndex(u => u.IsActive);
    }
}

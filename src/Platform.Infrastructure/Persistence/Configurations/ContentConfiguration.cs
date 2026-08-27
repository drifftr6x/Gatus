using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.ToTable("contents");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(c => c.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Url).HasColumnName("url").HasMaxLength(2000).IsRequired();
        builder.Property(c => c.ThumbnailUrl).HasColumnName("thumbnail_url").HasMaxLength(2000);
        builder.Property(c => c.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(c => c.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(c => c.MimeType).HasColumnName("mime_type").HasMaxLength(100);
        builder.Property(c => c.Checksum).HasColumnName("checksum").HasMaxLength(64);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");

        builder.HasOne(c => c.CreatedBy)
            .WithMany(u => u.CreatedContent)
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.Type);
        builder.HasIndex(c => c.IsActive);
        builder.HasIndex(c => c.CreatedById);
    }
}

public class ContentTagConfiguration : IEntityTypeConfiguration<ContentTag>
{
    public void Configure(EntityTypeBuilder<ContentTag> builder)
    {
        builder.ToTable("content_tags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.ContentId).HasColumnName("content_id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(50).IsRequired();

        builder.HasOne(t => t.Content)
            .WithMany(c => c.Tags)
            .HasForeignKey(t => t.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.ContentId);
        builder.HasIndex(t => t.Name);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class EnrollmentTokenConfiguration : IEntityTypeConfiguration<EnrollmentToken>
{
    public void Configure(EntityTypeBuilder<EnrollmentToken> builder)
    {
        builder.ToTable("enrollment_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(t => t.Label).HasColumnName("label").HasMaxLength(200);
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.IsUsed).HasColumnName("is_used");
        builder.Property(t => t.UsedAt).HasColumnName("used_at");
        builder.Property(t => t.UsedByDeviceId).HasColumnName("used_by_device_id");
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.IsRevoked).HasColumnName("is_revoked");

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.IsUsed);

        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

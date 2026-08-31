using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("platform_settings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasColumnName("key").HasMaxLength(100);
        builder.Property(s => s.Value).HasColumnName("value");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}

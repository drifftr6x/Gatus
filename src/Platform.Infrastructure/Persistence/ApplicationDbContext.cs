using Microsoft.EntityFrameworkCore;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceTelemetry> DeviceTelemetry => Set<DeviceTelemetry>();
    public DbSet<DeviceConnectivity> DeviceConnectivity => Set<DeviceConnectivity>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<ContentTag> ContentTags => Set<ContentTag>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<EnrollmentToken> EnrollmentTokens => Set<EnrollmentToken>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<DeviceConfigTemplate> DeviceConfigTemplates => Set<DeviceConfigTemplate>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<DeploymentResult> DeploymentResults => Set<DeploymentResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Device or Content or User or Schedule or DeviceGroup or DeviceConfigTemplate or NotificationChannel
                && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is Device device) device.CreatedAt = DateTime.UtcNow;
                else if (entry.Entity is Content content) content.CreatedAt = DateTime.UtcNow;
                else if (entry.Entity is User user) user.CreatedAt = DateTime.UtcNow;
                else if (entry.Entity is Schedule schedule) schedule.CreatedAt = DateTime.UtcNow;
                else if (entry.Entity is DeviceGroup group) group.CreatedAt = DateTime.UtcNow;
                else if (entry.Entity is DeviceConfigTemplate template) template.CreatedAt = DateTime.UtcNow;
                else if (entry.Entity is NotificationChannel channel) channel.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                if (entry.Entity is Device device) device.UpdatedAt = DateTime.UtcNow;
                else if (entry.Entity is Content content) content.UpdatedAt = DateTime.UtcNow;
                else if (entry.Entity is User user) user.UpdatedAt = DateTime.UtcNow;
                else if (entry.Entity is Schedule schedule) schedule.UpdatedAt = DateTime.UtcNow;
                else if (entry.Entity is DeviceGroup group) group.UpdatedAt = DateTime.UtcNow;
                else if (entry.Entity is DeviceConfigTemplate template) template.UpdatedAt = DateTime.UtcNow;
                else if (entry.Entity is NotificationChannel channel) channel.UpdatedAt = DateTime.UtcNow;
            }
            }

        return base.SaveChangesAsync(cancellationToken);
    }
}

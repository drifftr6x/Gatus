using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.DeviceId).HasColumnName("device_id");
        builder.Property(a => a.RuleId).HasColumnName("rule_id");
        builder.Property(a => a.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Message).HasColumnName("message").HasMaxLength(1000);
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.RaisedAt).HasColumnName("raised_at");
        builder.Property(a => a.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(a => a.AcknowledgedById).HasColumnName("acknowledged_by_id");
        builder.Property(a => a.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(a => a.AutoResolved).HasColumnName("auto_resolved");
        builder.Property(a => a.LastNotifiedAt).HasColumnName("last_notified_at");
        builder.Property(a => a.EscalationStep).HasColumnName("escalation_step");

        builder.HasOne(a => a.EscalationPolicy)
            .WithMany()
            .HasForeignKey("EscalationPolicyId")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Device)
            .WithMany()
            .HasForeignKey(a => a.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Rule)
            .WithMany(r => r.Alerts)
            .HasForeignKey(a => a.RuleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.AcknowledgedBy)
            .WithMany()
            .HasForeignKey(a => a.AcknowledgedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.DeviceId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.Severity);
        builder.HasIndex(a => a.RaisedAt);
        builder.HasIndex(a => new { a.DeviceId, a.RuleId, a.Status });
    }
}

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("alert_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.Metric).HasColumnName("metric").HasMaxLength(50).IsRequired();
        builder.Property(r => r.Operator).HasColumnName("operator").HasMaxLength(10).IsRequired();
        builder.Property(r => r.Threshold).HasColumnName("threshold");
        builder.Property(r => r.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.IsEnabled).HasColumnName("is_enabled");
        builder.Property(r => r.CooldownMinutes).HasColumnName("cooldown_minutes").HasDefaultValue(15);
        builder.Property(r => r.EscalationPolicyId).HasColumnName("escalation_policy_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        builder.HasOne<EscalationPolicy>()
            .WithMany(p => p.Rules)
            .HasForeignKey(r => r.EscalationPolicyId)
            .OnDelete(DeleteBehavior.SetNull);
        }
        }

        public class EscalationPolicyConfiguration : IEntityTypeConfiguration<EscalationPolicy>
        {
        public void Configure(EntityTypeBuilder<EscalationPolicy> builder)
        {
        builder.ToTable("escalation_policies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(p => p.IsEnabled).HasColumnName("is_enabled");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        }
        }

        public class EscalationStepConfiguration : IEntityTypeConfiguration<EscalationStep>
        {
        public void Configure(EntityTypeBuilder<EscalationStep> builder)
        {
        builder.ToTable("escalation_steps");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.PolicyId).HasColumnName("policy_id");
        builder.Property(s => s.Order).HasColumnName("order");
        builder.Property(s => s.DelayMinutes).HasColumnName("delay_minutes");
        builder.Property(s => s.ChannelId).HasColumnName("channel_id");
        builder.Property(s => s.EscalateSeverity).HasColumnName("escalate_severity").HasConversion<string>().HasMaxLength(20);

        builder.HasOne(s => s.Policy)
            .WithMany(p => p.Steps)
            .HasForeignKey(s => s.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Channel)
            .WithMany()
            .HasForeignKey(s => s.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);
        }
        }

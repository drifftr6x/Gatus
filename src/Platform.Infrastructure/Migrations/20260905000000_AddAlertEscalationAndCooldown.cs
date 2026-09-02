using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Adds alert cooldown, escalation policies, and escalation steps.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260905000000_AddAlertEscalationAndCooldown")]
public partial class AddAlertEscalationAndCooldown : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Alert cooldown + escalation tracking
        migrationBuilder.Sql("ALTER TABLE alerts ADD COLUMN IF NOT EXISTS last_notified_at timestamp with time zone;");
        migrationBuilder.Sql("ALTER TABLE alerts ADD COLUMN IF NOT EXISTS escalation_step integer NOT NULL DEFAULT 0;");
        migrationBuilder.Sql("ALTER TABLE alerts ADD COLUMN IF NOT EXISTS escalation_policy_id uuid;");
        migrationBuilder.Sql("ALTER TABLE alert_rules ADD COLUMN IF NOT EXISTS cooldown_minutes integer NOT NULL DEFAULT 15;");
        migrationBuilder.Sql("ALTER TABLE alert_rules ADD COLUMN IF NOT EXISTS escalation_policy_id uuid;");

        // Escalation policies
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS escalation_policies (
                id uuid NOT NULL PRIMARY KEY,
                name character varying(200) NOT NULL,
                description character varying(500),
                is_enabled boolean NOT NULL DEFAULT true,
                created_at timestamp with time zone NOT NULL DEFAULT now()
            );");

        // Escalation steps
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS escalation_steps (
                id uuid NOT NULL PRIMARY KEY,
                policy_id uuid NOT NULL REFERENCES escalation_policies(id) ON DELETE CASCADE,
                ""order"" integer NOT NULL,
                delay_minutes integer NOT NULL,
                channel_id uuid NOT NULL REFERENCES notification_channels(id) ON DELETE RESTRICT,
                escalate_severity character varying(20)
            );");

        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_escalation_steps_policy_id ON escalation_steps(policy_id);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS escalation_steps;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS escalation_policies;");
        migrationBuilder.Sql("ALTER TABLE alert_rules DROP COLUMN IF EXISTS escalation_policy_id;");
        migrationBuilder.Sql("ALTER TABLE alert_rules DROP COLUMN IF EXISTS cooldown_minutes;");
        migrationBuilder.Sql("ALTER TABLE alerts DROP COLUMN IF EXISTS escalation_policy_id;");
        migrationBuilder.Sql("ALTER TABLE alerts DROP COLUMN IF EXISTS escalation_step;");
        migrationBuilder.Sql("ALTER TABLE alerts DROP COLUMN IF EXISTS last_notified_at;");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Adds the kiosk policy columns introduced on Device after the last tracked migration.
/// The local database may have received these columns out-of-band; IF NOT EXISTS keeps
/// startup safe for both fresh and already-repaired development databases.
/// </summary>
public partial class AddKioskPolicyColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE devices ADD COLUMN IF NOT EXISTS kiosk_enabled boolean NOT NULL DEFAULT false;");
        migrationBuilder.Sql(
            "ALTER TABLE devices ADD COLUMN IF NOT EXISTS policy_json jsonb;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE devices DROP COLUMN IF EXISTS policy_json;");
        migrationBuilder.Sql("ALTER TABLE devices DROP COLUMN IF EXISTS kiosk_enabled;");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Adds rollout wave percentage and scheduled deployment support.
/// </summary>
public partial class AddDeploymentScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE deployments ADD COLUMN IF NOT EXISTS rollout_percent integer;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE deployments DROP COLUMN IF EXISTS rollout_percent;");
    }
}

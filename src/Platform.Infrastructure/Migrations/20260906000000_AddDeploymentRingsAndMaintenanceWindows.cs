using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Adds deployment ring chain fields (ring_order, parent_deployment_id, soak_minutes)
/// and maintenance window fields on device_groups.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906000000_AddDeploymentRingsAndMaintenanceWindows")]
public partial class AddDeploymentRingsAndMaintenanceWindows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE deployments ADD COLUMN IF NOT EXISTS ring_order integer;");
        migrationBuilder.Sql("ALTER TABLE deployments ADD COLUMN IF NOT EXISTS parent_deployment_id uuid REFERENCES deployments(id) ON DELETE SET NULL;");
        migrationBuilder.Sql("ALTER TABLE deployments ADD COLUMN IF NOT EXISTS soak_minutes integer;");
        migrationBuilder.Sql("ALTER TABLE device_groups ADD COLUMN IF NOT EXISTS maintenance_window_start time;");
        migrationBuilder.Sql("ALTER TABLE device_groups ADD COLUMN IF NOT EXISTS maintenance_window_duration_minutes integer;");
        migrationBuilder.Sql("ALTER TABLE device_groups ADD COLUMN IF NOT EXISTS maintenance_window_days character varying(50);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE deployments DROP COLUMN IF EXISTS ring_order;");
        migrationBuilder.Sql("ALTER TABLE deployments DROP COLUMN IF EXISTS parent_deployment_id;");
        migrationBuilder.Sql("ALTER TABLE deployments DROP COLUMN IF EXISTS soak_minutes;");
        migrationBuilder.Sql("ALTER TABLE device_groups DROP COLUMN IF EXISTS maintenance_window_start;");
        migrationBuilder.Sql("ALTER TABLE device_groups DROP COLUMN IF EXISTS maintenance_window_duration_minutes;");
        migrationBuilder.Sql("ALTER TABLE device_groups DROP COLUMN IF EXISTS maintenance_window_days;");
    }
}

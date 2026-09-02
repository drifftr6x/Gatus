using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Creates device_connectivity (ping monitor history) and adds deployments.RolloutPercent.
/// Both were previously applied manually in dev; required for fresh databases.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260901010000_AddDeviceConnectivityAndRolloutPercent")]
public partial class AddDeviceConnectivityAndRolloutPercent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS device_connectivity (
                id uuid NOT NULL PRIMARY KEY,
                device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
                timestamp timestamp with time zone NOT NULL,
                is_online boolean NOT NULL,
                response_time_ms integer,
                source character varying(20)
            );
            CREATE INDEX IF NOT EXISTS ix_device_connectivity_device_id_timestamp
                ON device_connectivity(device_id, timestamp);
        ");

        migrationBuilder.Sql(@"ALTER TABLE deployments ADD COLUMN IF NOT EXISTS ""RolloutPercent"" integer;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS device_connectivity;");
        migrationBuilder.Sql(@"ALTER TABLE deployments DROP COLUMN IF EXISTS ""RolloutPercent"";");
    }
}

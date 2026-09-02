using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Adds agent_updates table for signed agent self-update packages.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908000000_AddAgentUpdates")]
public partial class AddAgentUpdates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS agent_updates (
                id uuid PRIMARY KEY,
                version character varying(32) NOT NULL,
                sha256_checksum character varying(64) NOT NULL,
                file_size_bytes bigint NOT NULL,
                storage_path character varying(500) NOT NULL,
                rollout_percent integer NOT NULL DEFAULT 100,
                min_version character varying(32),
                notes character varying(2000),
                is_active boolean NOT NULL DEFAULT true,
                created_at timestamp with time zone NOT NULL,
                created_by_id uuid
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_agent_updates_version ON agent_updates (version);
            CREATE INDEX IF NOT EXISTS ix_agent_updates_is_active ON agent_updates (is_active);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS agent_updates;");
    }
}

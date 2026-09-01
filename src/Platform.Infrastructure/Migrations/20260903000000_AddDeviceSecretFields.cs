using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Migrations;

public partial class AddDeviceSecretFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE devices ADD COLUMN IF NOT EXISTS device_secret_hash character varying(64);");
        migrationBuilder.Sql("ALTER TABLE devices ADD COLUMN IF NOT EXISTS device_secret_issued_at timestamp with time zone;");
        migrationBuilder.Sql("ALTER TABLE devices ADD COLUMN IF NOT EXISTS device_secret_revoked_at timestamp with time zone;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE devices DROP COLUMN IF EXISTS device_secret_revoked_at;");
        migrationBuilder.Sql("ALTER TABLE devices DROP COLUMN IF EXISTS device_secret_issued_at;");
        migrationBuilder.Sql("ALTER TABLE devices DROP COLUMN IF EXISTS device_secret_hash;");
    }
}

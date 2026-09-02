using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Adds must_change_password flag on users for forced password change after seeding/provisioning.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907000000_AddUserMustChangePassword")]
public partial class AddUserMustChangePassword : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE users DROP COLUMN IF EXISTS must_change_password;");
    }
}

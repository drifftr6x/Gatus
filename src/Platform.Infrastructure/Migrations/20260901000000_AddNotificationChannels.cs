using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence;

#nullable disable

namespace Platform.Infrastructure.Migrations;

/// <summary>
/// Creates the notification_channels table. Previously applied manually in dev;
/// required so fresh databases (production compose) get the full schema.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260901000000_AddNotificationChannels")]
public partial class AddNotificationChannels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS notification_channels (
                id uuid NOT NULL PRIMARY KEY,
                name character varying(200) NOT NULL,
                type character varying(20) NOT NULL,
                config_json jsonb NOT NULL DEFAULT '{}',
                is_enabled boolean NOT NULL DEFAULT true,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                updated_at timestamp with time zone
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_notification_channels_name ON notification_channels(name);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS notification_channels;");
    }
}

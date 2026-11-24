using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingFeatureSwitchesAndGenericEventTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropCheckConstraint(
                name: "ck_event_type",
                schema: "events",
                table: "event");

            migrationBuilder.Sql(
                """
                UPDATE events.event SET type = 'Corporate' WHERE type IN ('Roadshow', 'Feria', 'Tradeshow');
                UPDATE events.event SET type = 'Conference' WHERE type = 'Webinar';
                UPDATE events.event SET type = 'Other' WHERE type NOT IN ('Corporate', 'Christmas', 'Wedding', 'Birthday', 'Concert', 'Graduation', 'Conference', 'Other');
                UPDATE events.raffle SET visual_style = 'roulette' WHERE visual_style = 'ball_tombola';
                UPDATE events.template
                SET body_html = REPLACE(
                    REPLACE(body_html, 'Ministerio de Turismo de la República Dominicana', '{{organizador}}'),
                    'Ministry of Tourism of the Dominican Republic',
                    '{{organizador}}')
                WHERE template_key = 'events.prize.won';
                """);

            migrationBuilder.CreateTable(
                name: "branding_setting",
                schema: "events",
                columns: table => new
                {
                    scope_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    app_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    app_subtitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    organization_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    logo_dark_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    primary_color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    accent_color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branding_setting", x => x.scope_key);
                });

            migrationBuilder.CreateTable(
                name: "feature_switch",
                schema: "events",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    changed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_switch", x => x.key);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle",
                sql: "visual_style IN ('roulette', 'sequential_names')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_type",
                schema: "events",
                table: "event",
                sql: "type IN ('Corporate', 'Christmas', 'Wedding', 'Birthday', 'Concert', 'Graduation', 'Conference', 'Other')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branding_setting",
                schema: "events");

            migrationBuilder.DropTable(
                name: "feature_switch",
                schema: "events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropCheckConstraint(
                name: "ck_event_type",
                schema: "events",
                table: "event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle",
                sql: "visual_style IN ('roulette', 'ball_tombola', 'sequential_names')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_type",
                schema: "events",
                table: "event",
                sql: "type IN ('Roadshow', 'Feria', 'Tradeshow', 'Webinar')");
        }
    }
}

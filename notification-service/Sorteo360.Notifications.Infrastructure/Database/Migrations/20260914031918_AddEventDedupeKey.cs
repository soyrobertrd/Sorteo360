using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Notifications.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEventDedupeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dedupe_key",
                schema: "notifications",
                table: "notification_event",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_notification_event_dedupe_key",
                schema: "notifications",
                table: "notification_event",
                column: "dedupe_key",
                unique: true,
                filter: "dedupe_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_notification_event_dedupe_key",
                schema: "notifications",
                table: "notification_event");

            migrationBuilder.DropColumn(
                name: "dedupe_key",
                schema: "notifications",
                table: "notification_event");
        }
    }
}

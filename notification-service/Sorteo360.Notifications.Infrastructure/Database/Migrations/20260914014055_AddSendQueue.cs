using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Notifications.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSendQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_payload",
                schema: "notifications",
                columns: table => new
                {
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    render_values = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_payload", x => x.recipient_id);
                    table.ForeignKey(
                        name: "fk_notification_payload_recipient",
                        column: x => x.recipient_id,
                        principalSchema: "notifications",
                        principalTable: "notification_recipient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_suppression",
                schema: "notifications",
                columns: table => new
                {
                    email = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_suppression", x => x.email);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_payload",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_suppression",
                schema: "notifications");
        }
    }
}

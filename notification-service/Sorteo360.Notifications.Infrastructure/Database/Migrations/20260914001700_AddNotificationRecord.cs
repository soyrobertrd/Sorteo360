using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Notifications.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "notification_batch",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    from_address = table.Column<string>(type: "text", nullable: false),
                    from_name = table.Column<string>(type: "text", nullable: true),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    body_sha256 = table.Column<string>(type: "text", nullable: false),
                    tracking_expected = table.Column<bool>(type: "boolean", nullable: false),
                    trace_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_batch", x => x.id);
                    table.CheckConstraint("ck_notification_batch_language", "language IN ('es', 'en', 'fr', 'de', 'pt', 'it')");
                });

            migrationBuilder.CreateTable(
                name: "notification_about",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_service = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_about", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_about_batch",
                        column: x => x.batch_id,
                        principalSchema: "notifications",
                        principalTable: "notification_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_recipient",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    delivery_status = table.Column<string>(type: "text", nullable: false),
                    recipient_action = table.Column<string>(type: "text", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    sg_message_id = table.Column<string>(type: "text", nullable: true),
                    smtp_queued_as = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_recipient", x => x.id);
                    table.CheckConstraint("ck_notification_recipient_action", "recipient_action IN ('complained', 'none', 'unsubscribed')");
                    table.CheckConstraint("ck_notification_recipient_delivery_status", "delivery_status IN ('bounced', 'deferred', 'delivered', 'dropped', 'failed', 'queued', 'sent')");
                    table.ForeignKey(
                        name: "fk_notification_recipient_batch",
                        column: x => x.batch_id,
                        principalSchema: "notifications",
                        principalTable: "notification_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_event",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sg_event_id = table.Column<string>(type: "text", nullable: true),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_event_recipient",
                        column: x => x.recipient_id,
                        principalSchema: "notifications",
                        principalTable: "notification_recipient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_about_batch_id",
                schema: "notifications",
                table: "notification_about",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_about_entity",
                schema: "notifications",
                table: "notification_about",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_batch_created_at",
                schema: "notifications",
                table: "notification_batch",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notification_batch_type",
                schema: "notifications",
                table: "notification_batch",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ux_notification_batch_idempotency_key",
                schema: "notifications",
                table: "notification_batch",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_event_recipient_id",
                schema: "notifications",
                table: "notification_event",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ux_notification_event_sg_event_id",
                schema: "notifications",
                table: "notification_event",
                column: "sg_event_id",
                unique: true,
                filter: "sg_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_batch_id",
                schema: "notifications",
                table: "notification_recipient",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_email",
                schema: "notifications",
                table: "notification_recipient",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_pending",
                schema: "notifications",
                table: "notification_recipient",
                columns: new[] { "delivery_status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_about",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_event",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_recipient",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_batch",
                schema: "notifications");
        }
    }
}

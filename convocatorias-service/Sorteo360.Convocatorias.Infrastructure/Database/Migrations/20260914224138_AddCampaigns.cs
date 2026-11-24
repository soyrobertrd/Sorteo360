using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    launched_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    launched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign", x => x.id);
                    table.CheckConstraint("ck_campaign_language", "language IN ('de', 'en', 'es', 'fr', 'it', 'pt')");
                    table.CheckConstraint("ck_campaign_launch_stamp_is_whole", "(launched_by_id IS NULL) = (launched_at IS NULL)");
                    table.CheckConstraint("ck_campaign_sent_requires_launch", "sent_at IS NULL OR launched_at IS NOT NULL");
                    table.CheckConstraint("ck_campaign_state", "state IN ('cancelled', 'draft', 'sending', 'sent')");
                });

            migrationBuilder.CreateTable(
                name: "campaign_exclusion",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    excluded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_exclusion", x => x.id);
                    table.CheckConstraint("ck_campaign_exclusion_reason", "reason IN ('contact_unavailable', 'do_not_invite', 'no_email', 'opted_out')");
                    table.ForeignKey(
                        name: "FK_campaign_exclusion_campaign_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "convocatorias",
                        principalTable: "campaign",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_recipient",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    send_status = table.Column<string>(type: "text", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    registration_token_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    unsubscribe_token_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    unsubscribed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unsubscribe_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_recipient", x => x.id);
                    table.CheckConstraint("ck_campaign_recipient_send_status", "send_status IN ('failed', 'handed_off', 'pending', 'suppressed')");
                    table.CheckConstraint("ck_campaign_recipient_sync_requires_unsubscribe", "unsubscribe_synced_at IS NULL OR unsubscribed_at IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_campaign_recipient_campaign_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "convocatorias",
                        principalTable: "campaign",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campaign_event_id",
                schema: "convocatorias",
                table: "campaign",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_state",
                schema: "convocatorias",
                table: "campaign",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ux_campaign_exclusion_campaign_contact",
                schema: "convocatorias",
                table: "campaign_exclusion",
                columns: new[] { "campaign_id", "contact_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipient_campaign_send_status",
                schema: "convocatorias",
                table: "campaign_recipient",
                columns: new[] { "campaign_id", "send_status" });

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipient_registration_token_hash",
                schema: "convocatorias",
                table: "campaign_recipient",
                column: "registration_token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipient_unsubscribe_token_hash",
                schema: "convocatorias",
                table: "campaign_recipient",
                column: "unsubscribe_token_hash");

            migrationBuilder.CreateIndex(
                name: "ux_campaign_recipient_campaign_contact",
                schema: "convocatorias",
                table: "campaign_recipient",
                columns: new[] { "campaign_id", "contact_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_exclusion",
                schema: "convocatorias");

            migrationBuilder.DropTable(
                name: "campaign_recipient",
                schema: "convocatorias");

            migrationBuilder.DropTable(
                name: "campaign",
                schema: "convocatorias");
        }
    }
}

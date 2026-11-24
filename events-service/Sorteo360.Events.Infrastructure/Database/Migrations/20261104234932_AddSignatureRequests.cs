using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSignatureRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "signature_request",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_sequential = table.Column<bool>(type: "boolean", nullable: false),
                    sender_cedula = table.Column<string>(type: "text", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    public_access_id = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    failure_code = table.Column<string>(type: "text", nullable: true),
                    send_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_request", x => x.id);
                    table.CheckConstraint("ck_signature_request_send_attempts", "send_attempts >= 0");
                    table.CheckConstraint("ck_signature_request_state", "state IN ('Queued', 'Sent', 'Completing', 'Signed', 'NotSent', 'SendUncertain', 'DownloadLost', 'Rejected', 'Expired')");
                });

            migrationBuilder.CreateTable(
                name: "signature_request_file",
                schema: "events",
                columns: table => new
                {
                    signature_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_request_file", x => new { x.signature_request_id, x.role, x.position });
                    table.CheckConstraint("ck_signature_request_file_position", "position >= 0");
                    table.CheckConstraint("ck_signature_request_file_role", "role IN ('Sent', 'Signed')");
                    table.ForeignKey(
                        name: "fk_signature_request_file_signature_request",
                        column: x => x.signature_request_id,
                        principalSchema: "events",
                        principalTable: "signature_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_signature_request_file_stored_file",
                        column: x => x.stored_file_id,
                        principalSchema: "events",
                        principalTable: "stored_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "signature_request_signer",
                schema: "events",
                columns: table => new
                {
                    signature_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    cedula = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    signing_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_request_signer", x => new { x.signature_request_id, x.position });
                    table.CheckConstraint("ck_signature_request_signer_action", "action IN ('Sign', 'Approval')");
                    table.CheckConstraint("ck_signature_request_signer_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_signature_request_signer_signature_request",
                        column: x => x.signature_request_id,
                        principalSchema: "events",
                        principalTable: "signature_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_signature_request_due",
                schema: "events",
                table: "signature_request",
                column: "next_attempt_at",
                filter: "state IN ('Queued', 'Completing')");

            migrationBuilder.CreateIndex(
                name: "ix_signature_request_file_stored_file_id",
                schema: "events",
                table: "signature_request_file",
                column: "stored_file_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "signature_request_file",
                schema: "events");

            migrationBuilder.DropTable(
                name: "signature_request_signer",
                schema: "events");

            migrationBuilder.DropTable(
                name: "signature_request",
                schema: "events");
        }
    }
}

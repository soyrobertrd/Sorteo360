using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign_upload_batch",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "text", nullable: false),
                    detected_encoding = table.Column<string>(type: "text", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    matched_count = table.Column<int>(type: "integer", nullable: false),
                    unmatched_count = table.Column<int>(type: "integer", nullable: false),
                    suppressed_count = table.Column<int>(type: "integer", nullable: false),
                    already_recipient_count = table.Column<int>(type: "integer", nullable: false),
                    duplicate_in_file_count = table.Column<int>(type: "integer", nullable: false),
                    malformed_count = table.Column<int>(type: "integer", nullable: false),
                    collapsed_duplicate_count = table.Column<int>(type: "integer", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_upload_batch", x => x.id);
                    table.CheckConstraint("ck_campaign_upload_batch_confirmation_is_whole", "(confirmed_by_id IS NULL) = (confirmed_at IS NULL)");
                    table.CheckConstraint("ck_campaign_upload_batch_counts_add_up", "matched_count + unmatched_count + suppressed_count + already_recipient_count\n+ duplicate_in_file_count + malformed_count + collapsed_duplicate_count = row_count");
                    table.ForeignKey(
                        name: "FK_campaign_upload_batch_campaign_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "convocatorias",
                        principalTable: "campaign",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_upload_row",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_row_number = table.Column<int>(type: "integer", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    company_raw = table.Column<string>(type: "text", nullable: true),
                    bucket = table.Column<string>(type: "text", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duplicate_row_numbers = table.Column<int[]>(type: "integer[]", nullable: false),
                    collapsed_duplicates = table.Column<int>(type: "integer", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_upload_row", x => x.id);
                    table.CheckConstraint("ck_campaign_upload_row_applied_has_contact", "applied_at IS NULL OR contact_id IS NOT NULL");
                    table.CheckConstraint("ck_campaign_upload_row_bucket", "bucket IN ('already_a_recipient', 'duplicate_in_file', 'malformed', 'matched', 'suppressed', 'unmatched')");
                    table.CheckConstraint("ck_campaign_upload_row_reason_code", "reason_code IS NULL OR reason_code IN ('contact_unavailable', 'do_not_invite', 'invalid_email', 'missing_email', 'missing_name', 'no_email', 'opted_out')");
                    table.ForeignKey(
                        name: "FK_campaign_upload_row_campaign_upload_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "convocatorias",
                        principalTable: "campaign_upload_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_campaign_upload_batch_pending_per_campaign",
                schema: "convocatorias",
                table: "campaign_upload_batch",
                column: "campaign_id",
                unique: true,
                filter: "confirmed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_upload_row_batch_source_row",
                schema: "convocatorias",
                table: "campaign_upload_row",
                columns: new[] { "batch_id", "source_row_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_upload_row",
                schema: "convocatorias");

            migrationBuilder.DropTable(
                name: "campaign_upload_batch",
                schema: "convocatorias");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prize_handover",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prize_draw_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    receiver_identity_document = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    receiver_is_winner = table.Column<bool>(type: "boolean", nullable: false),
                    signature_stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    redeemed_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prize_handover", x => x.id);
                    table.CheckConstraint("ck_prize_handover_reversal_complete", "(reversed_at IS NULL AND reversed_by_id IS NULL AND reversal_reason IS NULL) OR (reversed_at IS NOT NULL AND reversed_by_id IS NOT NULL AND reversal_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_prize_handover_prize_draw",
                        column: x => x.prize_draw_id,
                        principalSchema: "events",
                        principalTable: "prize_draw",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prize_handover_stored_file",
                        column: x => x.signature_stored_file_id,
                        principalSchema: "events",
                        principalTable: "stored_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prize_handover_signature_stored_file_id",
                schema: "events",
                table: "prize_handover",
                column: "signature_stored_file_id");

            migrationBuilder.CreateIndex(
                name: "ux_prize_handover_prize_draw_id_live",
                schema: "events",
                table: "prize_handover",
                column: "prize_draw_id",
                unique: true,
                filter: "reversed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prize_handover",
                schema: "events");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeDraw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prize_draw",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prize_id = table.Column<Guid>(type: "uuid", nullable: false),
                    winner_attendee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    winner_name = table.Column<string>(type: "text", nullable: false),
                    winner_company = table.Column<string>(type: "text", nullable: true),
                    winner_email = table.Column<string>(type: "text", nullable: false),
                    drawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    drawn_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eligible_pool_size = table.Column<int>(type: "integer", nullable: false),
                    redemption_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    void_code = table.Column<string>(type: "text", nullable: true),
                    void_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prize_draw", x => x.id);
                    table.CheckConstraint("ck_prize_draw_eligible_pool_size", "eligible_pool_size > 0");
                    table.CheckConstraint("ck_prize_draw_redemption_code", "redemption_code ~ '^RD-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$'");
                    table.CheckConstraint("ck_prize_draw_void_code", "void_code IN ('absent', 'declined', 'ineligible', 'other')");
                    table.CheckConstraint("ck_prize_draw_void_complete", "(void_code IS NULL AND voided_at IS NULL AND voided_by_id IS NULL) OR (void_code IS NOT NULL AND voided_at IS NOT NULL AND voided_by_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_prize_draw_prize",
                        column: x => x.prize_id,
                        principalSchema: "events",
                        principalTable: "prize",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prize_draw_raffle_attendee",
                        column: x => x.winner_attendee_id,
                        principalSchema: "events",
                        principalTable: "raffle_attendee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prize_draw_winner_attendee_id",
                schema: "events",
                table: "prize_draw",
                column: "winner_attendee_id");

            migrationBuilder.CreateIndex(
                name: "ux_prize_draw_prize_id_live",
                schema: "events",
                table: "prize_draw",
                column: "prize_id",
                unique: true,
                filter: "voided_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_prize_draw_redemption_code",
                schema: "events",
                table: "prize_draw",
                column: "redemption_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prize_draw",
                schema: "events");
        }
    }
}

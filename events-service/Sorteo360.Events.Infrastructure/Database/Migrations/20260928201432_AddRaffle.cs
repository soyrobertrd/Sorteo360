using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaffle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "raffle",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    visual_style = table.Column<string>(type: "text", nullable: false),
                    show_prize_on_screen = table.Column<bool>(type: "boolean", nullable: false),
                    animation_seconds = table.Column<int>(type: "integer", nullable: false),
                    requires_presence_confirmation = table.Column<bool>(type: "boolean", nullable: false),
                    one_prize_per_person = table.Column<bool>(type: "boolean", nullable: false),
                    one_prize_per_company = table.Column<bool>(type: "boolean", nullable: false),
                    notary_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notary_identity_document = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    witness_one_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    witness_one_identity_document = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    witness_two_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    witness_two_identity_document = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raffle", x => x.id);
                    table.CheckConstraint("ck_raffle_animation_seconds", "animation_seconds BETWEEN 5 AND 30");
                    table.CheckConstraint("ck_raffle_state", "state IN ('draft', 'in_progress', 'completed')");
                    table.CheckConstraint("ck_raffle_visual_style", "visual_style IN ('roulette', 'ball_tombola', 'sequential_names')");
                    table.ForeignKey(
                        name: "fk_raffle_event",
                        column: x => x.event_id,
                        principalSchema: "events",
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_raffle_event_id",
                schema: "events",
                table: "raffle",
                column: "event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raffle",
                schema: "events");
        }
    }
}

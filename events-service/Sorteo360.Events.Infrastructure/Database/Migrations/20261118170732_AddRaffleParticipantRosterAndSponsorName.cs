using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaffleParticipantRosterAndSponsorName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_prize_sponsor_logo_needs_company",
                schema: "events",
                table: "prize");

            migrationBuilder.AddColumn<string>(
                name: "sponsor_name",
                schema: "events",
                table: "prize",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "raffle_participant",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email_normalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    employee_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    qr_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    checked_in = table.Column<bool>(type: "boolean", nullable: false),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raffle_participant", x => x.id);
                    table.ForeignKey(
                        name: "fk_raffle_participant_event",
                        column: x => x.event_id,
                        principalSchema: "events",
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_prize_sponsor_logo_needs_company",
                schema: "events",
                table: "prize",
                sql: "sponsor_logo_file_id IS NULL OR sponsor_company_id IS NOT NULL OR sponsor_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_raffle_participant_event_employee_code",
                schema: "events",
                table: "raffle_participant",
                columns: new[] { "event_id", "employee_code" });

            migrationBuilder.CreateIndex(
                name: "ux_raffle_participant_event_email",
                schema: "events",
                table: "raffle_participant",
                columns: new[] { "event_id", "email_normalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_raffle_participant_event_qr_code",
                schema: "events",
                table: "raffle_participant",
                columns: new[] { "event_id", "qr_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raffle_participant",
                schema: "events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_prize_sponsor_logo_needs_company",
                schema: "events",
                table: "prize");

            migrationBuilder.DropColumn(
                name: "sponsor_name",
                schema: "events",
                table: "prize");

            migrationBuilder.AddCheckConstraint(
                name: "ck_prize_sponsor_logo_needs_company",
                schema: "events",
                table: "prize",
                sql: "sponsor_logo_file_id IS NULL OR sponsor_company_id IS NOT NULL");
        }
    }
}

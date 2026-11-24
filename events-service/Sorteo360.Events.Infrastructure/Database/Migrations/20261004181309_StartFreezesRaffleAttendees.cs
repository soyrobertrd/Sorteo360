using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class StartFreezesRaffleAttendees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "awards_batch_id",
                schema: "events",
                table: "raffle",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "started_at",
                schema: "events",
                table: "raffle",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "started_by_id",
                schema: "events",
                table: "raffle",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "raffle_attendee",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raffle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_normalized = table.Column<string>(type: "text", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    company = table.Column<string>(type: "text", nullable: true),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    qualified = table.Column<bool>(type: "boolean", nullable: false),
                    exclusion_reason = table.Column<string>(type: "text", nullable: true),
                    manual_decision = table.Column<string>(type: "text", nullable: true),
                    manual_decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manual_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    manual_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    overruled_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raffle_attendee", x => x.id);
                    table.CheckConstraint("ck_raffle_attendee_exclusion_reason", "exclusion_reason IN ('walk_in', 'contact_unresolved', 'suspended', 'do_not_invite', 'manually_excluded', 'not_awards_member', 'dominican_domain')");
                    table.CheckConstraint("ck_raffle_attendee_manual_decision", "manual_decision IN ('included', 'excluded')");
                    table.CheckConstraint("ck_raffle_attendee_overruled_reason", "overruled_reason IN ('walk_in', 'contact_unresolved', 'suspended', 'do_not_invite', 'manually_excluded', 'not_awards_member', 'dominican_domain')");
                    table.CheckConstraint("ck_raffle_attendee_qualified", "qualified = (exclusion_reason IS NULL)");
                    table.ForeignKey(
                        name: "fk_raffle_attendee_raffle",
                        column: x => x.raffle_id,
                        principalSchema: "events",
                        principalTable: "raffle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_started",
                schema: "events",
                table: "raffle",
                sql: "(started_at IS NULL) = (started_by_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ux_raffle_attendee_raffle_email",
                schema: "events",
                table: "raffle_attendee",
                columns: new[] { "raffle_id", "email_normalized" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raffle_attendee",
                schema: "events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_started",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "awards_batch_id",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "started_by_id",
                schema: "events",
                table: "raffle");
        }
    }
}

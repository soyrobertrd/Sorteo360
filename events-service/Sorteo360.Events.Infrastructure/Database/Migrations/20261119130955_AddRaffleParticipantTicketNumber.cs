using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaffleParticipantTicketNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ticket_number",
                schema: "events",
                table: "raffle_participant",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    rec RECORD;
                    candidate text;
                    raw_num integer;
                BEGIN
                    FOR rec IN
                        SELECT id, event_id
                        FROM events.raffle_participant
                        WHERE ticket_number IS NULL OR ticket_number = ''
                    LOOP
                        LOOP
                            raw_num := floor(random() * 1000000)::integer;
                            candidate := lpad((raw_num / 1000)::text, 3, '0') || '-' || lpad((raw_num % 1000)::text, 3, '0');
                            EXIT WHEN NOT EXISTS (
                                SELECT 1
                                FROM events.raffle_participant
                                WHERE event_id = rec.event_id AND ticket_number = candidate
                            );
                        END LOOP;

                        UPDATE events.raffle_participant
                        SET ticket_number = candidate
                        WHERE id = rec.id;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_raffle_participant_event_ticket_number",
                schema: "events",
                table: "raffle_participant",
                columns: new[] { "event_id", "ticket_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_raffle_participant_event_ticket_number",
                schema: "events",
                table: "raffle_participant");

            migrationBuilder.DropColumn(
                name: "ticket_number",
                schema: "events",
                table: "raffle_participant");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSyntheticEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_raffle_participant_event_email",
                schema: "events",
                table: "raffle_participant");

            migrationBuilder.RenameColumn(
                name: "email_normalized",
                schema: "events",
                table: "raffle_attendee_override",
                newName: "identity_key");

            migrationBuilder.RenameIndex(
                name: "ux_raffle_attendee_override_raffle_email",
                schema: "events",
                table: "raffle_attendee_override",
                newName: "ux_raffle_attendee_override_raffle_identity_key");

            migrationBuilder.RenameColumn(
                name: "email_normalized",
                schema: "events",
                table: "raffle_attendee",
                newName: "identity_key");

            migrationBuilder.RenameIndex(
                name: "ux_raffle_attendee_raffle_email",
                schema: "events",
                table: "raffle_attendee",
                newName: "ux_raffle_attendee_raffle_identity_key");

            migrationBuilder.RenameColumn(
                name: "winner_email",
                schema: "events",
                table: "prize_draw",
                newName: "winner_identity_key");

            migrationBuilder.AlterColumn<string>(
                name: "email_normalized",
                schema: "events",
                table: "raffle_participant",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<string>(
                name: "identity_key",
                schema: "events",
                table: "raffle_participant",
                type: "character varying(330)",
                maxLength: 330,
                nullable: false,
                defaultValue: "");

            // Datos: la clave sale del correo real, o del ID si el correo era inventado, o de la fila
            // si no había ninguno (legacy:). Las tablas que guardaban el correo inventado como
            // identidad pasan a la clave del padrón; al final el correo inventado se borra.
            migrationBuilder.Sql("""
                UPDATE events.raffle_participant SET identity_key = CASE
                    WHEN email_normalized NOT ILIKE '%@sorteo.local' THEN email_normalized
                    WHEN NULLIF(btrim(employee_code), '') IS NOT NULL THEN 'id:' || lower(btrim(employee_code))
                    ELSE 'legacy:' || id::text
                END;

                -- Dos filas del mismo evento con el mismo ID (solo en datos viejos): la segunda queda legacy.
                UPDATE events.raffle_participant p SET identity_key = 'legacy:' || p.id::text
                FROM (
                    SELECT id, row_number() OVER (PARTITION BY event_id, identity_key ORDER BY created_at, id) AS rn
                    FROM events.raffle_participant
                ) d
                WHERE d.id = p.id AND d.rn > 1;

                UPDATE events.raffle_attendee a SET identity_key = p.identity_key
                FROM events.raffle r
                JOIN events.raffle_participant p ON p.event_id = r.event_id
                WHERE a.raffle_id = r.id AND a.identity_key ILIKE '%@sorteo.local' AND p.email_normalized = a.identity_key;

                UPDATE events.raffle_attendee SET identity_key = 'legacy:' || id::text
                WHERE identity_key ILIKE '%@sorteo.local';

                UPDATE events.raffle_attendee_override o SET identity_key = p.identity_key
                FROM events.raffle r
                JOIN events.raffle_participant p ON p.event_id = r.event_id
                WHERE o.raffle_id = r.id AND o.identity_key ILIKE '%@sorteo.local' AND p.email_normalized = o.identity_key;

                UPDATE events.raffle_attendee_override SET identity_key = 'legacy:' || id::text
                WHERE identity_key ILIKE '%@sorteo.local';

                UPDATE events.prize_draw d SET winner_identity_key = a.identity_key
                FROM events.raffle_attendee a
                WHERE a.id = d.winner_attendee_id AND d.winner_identity_key ILIKE '%@sorteo.local';

                UPDATE events.prize_draw SET winner_identity_key = 'legacy:' || winner_attendee_id::text
                WHERE winner_identity_key ILIKE '%@sorteo.local';

                UPDATE events.raffle_participant SET email_normalized = NULL
                WHERE email_normalized ILIKE '%@sorteo.local';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_raffle_participant_event_email",
                schema: "events",
                table: "raffle_participant",
                columns: new[] { "event_id", "email_normalized" });

            migrationBuilder.CreateIndex(
                name: "ux_raffle_participant_event_identity_key",
                schema: "events",
                table: "raffle_participant",
                columns: new[] { "event_id", "identity_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_raffle_participant_event_email",
                schema: "events",
                table: "raffle_participant");

            migrationBuilder.DropIndex(
                name: "ux_raffle_participant_event_identity_key",
                schema: "events",
                table: "raffle_participant");

            // Vuelta atrás con pérdida: quien no tiene correo recibe otra vez uno inventado, y las
            // demás tablas vuelven a guardar un correo como identidad.
            migrationBuilder.Sql("""
                UPDATE events.raffle_participant
                SET email_normalized = 'participante-' || replace(id::text, '-', '') || '@sorteo.local'
                WHERE email_normalized IS NULL;

                UPDATE events.raffle_attendee a SET identity_key = p.email_normalized
                FROM events.raffle r
                JOIN events.raffle_participant p ON p.event_id = r.event_id
                WHERE a.raffle_id = r.id AND p.identity_key = a.identity_key AND p.email_normalized <> a.identity_key;

                UPDATE events.raffle_attendee SET identity_key = 'participante-' || replace(id::text, '-', '') || '@sorteo.local'
                WHERE identity_key LIKE 'id:%' OR identity_key LIKE 'legacy:%';

                UPDATE events.raffle_attendee_override o SET identity_key = p.email_normalized
                FROM events.raffle r
                JOIN events.raffle_participant p ON p.event_id = r.event_id
                WHERE o.raffle_id = r.id AND p.identity_key = o.identity_key AND p.email_normalized <> o.identity_key;

                DELETE FROM events.raffle_attendee_override
                WHERE identity_key LIKE 'id:%' OR identity_key LIKE 'legacy:%';

                UPDATE events.prize_draw d SET winner_identity_key = a.identity_key
                FROM events.raffle_attendee a
                WHERE a.id = d.winner_attendee_id AND (d.winner_identity_key LIKE 'id:%' OR d.winner_identity_key LIKE 'legacy:%');
                """);

            migrationBuilder.DropColumn(
                name: "identity_key",
                schema: "events",
                table: "raffle_participant");

            migrationBuilder.RenameColumn(
                name: "identity_key",
                schema: "events",
                table: "raffle_attendee_override",
                newName: "email_normalized");

            migrationBuilder.RenameIndex(
                name: "ux_raffle_attendee_override_raffle_identity_key",
                schema: "events",
                table: "raffle_attendee_override",
                newName: "ux_raffle_attendee_override_raffle_email");

            migrationBuilder.RenameColumn(
                name: "identity_key",
                schema: "events",
                table: "raffle_attendee",
                newName: "email_normalized");

            migrationBuilder.RenameIndex(
                name: "ux_raffle_attendee_raffle_identity_key",
                schema: "events",
                table: "raffle_attendee",
                newName: "ux_raffle_attendee_raffle_email");

            migrationBuilder.RenameColumn(
                name: "winner_identity_key",
                schema: "events",
                table: "prize_draw",
                newName: "winner_email");

            migrationBuilder.AlterColumn<string>(
                name: "email_normalized",
                schema: "events",
                table: "raffle_participant",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_raffle_participant_event_email",
                schema: "events",
                table: "raffle_participant",
                columns: new[] { "event_id", "email_normalized" },
                unique: true);
        }
    }
}

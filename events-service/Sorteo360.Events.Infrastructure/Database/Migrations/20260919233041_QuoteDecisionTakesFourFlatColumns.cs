using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <summary>
    /// El intento de decisión deja de guardar un puntero y pasa a guardar cuatro columnas planas.
    /// <para>
    /// Un intento se abre con el aviso del sometimiento y se cierra con el del desenlace, así que
    /// es dueño de dos envíos y una sola columna guarda uno solo: <c>approval_ref_id</c> no se
    /// podía renombrar y ya está (ADR-0011, enmienda 2026-09-13). Se renombra a
    /// <c>submit_notification_id</c> —que es lo único que esa columna llegó a guardar, el GUID del
    /// gate stub— y se suman su estado y el par del desenlace.
    /// </para>
    /// <para>
    /// Las tres columnas nuevas nacen vacías y nadie las llena todavía: esta migración acompaña a
    /// la tarjeta que pone el esquema, no a las que mandan correo.
    /// </para>
    /// </summary>
    public partial class QuoteDecisionTakesFourFlatColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renombrar y no recrear es lo que conserva el puntero de cada fila existente. EF
            // había emparejado esta columna con submit_notification_status, que habría metido un
            // GUID en una columna de estado y reventado su CHECK en la primera fila vieja.
            migrationBuilder.RenameColumn(
                name: "approval_ref_id",
                schema: "events",
                table: "quote_decision",
                newName: "submit_notification_id");

            migrationBuilder.AddColumn<string>(
                name: "submit_notification_status",
                schema: "events",
                table: "quote_decision",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outcome_notification_id",
                schema: "events",
                table: "quote_decision",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outcome_notification_status",
                schema: "events",
                table: "quote_decision",
                type: "text",
                nullable: true);

            // Null sigue siendo válido a propósito: es lo que dice toda fila escrita antes de que
            // existiera el correo, y lo que dirá un intento mientras su aviso no se haya mandado.
            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_decision_submit_notification_status",
                schema: "events",
                table: "quote_decision",
                sql: "submit_notification_status IS NULL OR submit_notification_status IN ('sent', 'no_recipient', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_decision_outcome_notification_status",
                schema: "events",
                table: "quote_decision",
                sql: "outcome_notification_status IS NULL OR outcome_notification_status IN ('sent', 'no_recipient', 'failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_decision_outcome_notification_status",
                schema: "events",
                table: "quote_decision");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_decision_submit_notification_status",
                schema: "events",
                table: "quote_decision");

            migrationBuilder.DropColumn(
                name: "outcome_notification_status",
                schema: "events",
                table: "quote_decision");

            migrationBuilder.DropColumn(
                name: "outcome_notification_id",
                schema: "events",
                table: "quote_decision");

            migrationBuilder.DropColumn(
                name: "submit_notification_status",
                schema: "events",
                table: "quote_decision");

            migrationBuilder.RenameColumn(
                name: "submit_notification_id",
                schema: "events",
                table: "quote_decision",
                newName: "approval_ref_id");
        }
    }
}

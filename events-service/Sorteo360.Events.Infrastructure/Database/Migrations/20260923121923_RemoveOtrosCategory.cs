using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOtrosCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El cliente quitó la categoría Otros el 2026-09-23 (ADR-0019). Antes de que los CHECK
            // la prohíban hay que sacar sus filas, y se borran de verdad: una fila con borrado
            // lógico sigue llevando la categoría y el CHECK nuevo no se crearía sobre ella. Todavía
            // no hay producción.
            //
            // Caen las fichas Otros, las cotizaciones Otros y todo lo que cuelga de ellas, de las
            // hojas hacia la raíz porque ninguna clave foránea borra en cascada. Un candidato cae
            // si su cotización es Otros o si su ficha lo es, aunque la cotización no lo sea: la
            // clave hacia service_supplier no deja borrar la ficha con candidatos encima.
            //
            // stored_file y los bytes no se tocan: fotos y adjuntos se desenlazan, nunca se borran
            // (ADR-0014), y el barrido de retención es quien decide sobre lo que quede huérfano.
            // quote_decision.winner_proposal_id no tiene clave foránea, así que una decisión de
            // una cotización que no es Otros cuya ganadora fuera una oferta de ficha Otros queda
            // apuntando a una fila que ya no existe; el conteo previo a stage lo revisa.
            migrationBuilder.Sql("""
                DELETE FROM events.proposal_attachment
                WHERE candidate_proposal_id IN (
                    SELECT id FROM events.candidate_proposal
                    WHERE quote_id IN (SELECT id FROM events.quote WHERE category = 'Otros')
                       OR candidate_id IN (
                           SELECT id FROM events.candidate
                           WHERE quote_id IN (SELECT id FROM events.quote WHERE category = 'Otros')
                              OR service_supplier_id IN (
                                  SELECT id FROM events.service_supplier WHERE category = 'Otros')));

                DELETE FROM events.candidate_proposal
                WHERE quote_id IN (SELECT id FROM events.quote WHERE category = 'Otros')
                   OR candidate_id IN (
                       SELECT id FROM events.candidate
                       WHERE quote_id IN (SELECT id FROM events.quote WHERE category = 'Otros')
                          OR service_supplier_id IN (
                              SELECT id FROM events.service_supplier WHERE category = 'Otros'));

                DELETE FROM events.candidate
                WHERE quote_id IN (SELECT id FROM events.quote WHERE category = 'Otros')
                   OR service_supplier_id IN (
                       SELECT id FROM events.service_supplier WHERE category = 'Otros');

                DELETE FROM events.quote_decision
                WHERE quote_id IN (SELECT id FROM events.quote WHERE category = 'Otros');

                DELETE FROM events.quote
                WHERE category = 'Otros';

                DELETE FROM events.service_supplier_photo
                WHERE service_supplier_id IN (
                    SELECT id FROM events.service_supplier WHERE category = 'Otros');

                DELETE FROM events.service_supplier
                WHERE category = 'Otros';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo vuelven los CHECK de dieciséis valores: las filas Otros borradas no se
            // reconstruyen, porque no queda de dónde sacarlas.
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica', 'Otros')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica', 'Otros')");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ASupplierIsACandidateOnce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las filas escritas antes de esta regla pueden tener al mismo proveedor dos veces en
            // una cotización, y el índice no se crea sobre datos así. Se conserva el candidato más
            // antiguo del proveedor y sus repetidos le entregan las propuestas vivas: eso es
            // exactamente la forma correcta —un candidato con varias ofertas— así que no se pierde
            // ningún precio, y los adjuntos viajan con la propuesta de la que cuelgan. Las
            // propuestas ya eliminadas se quedan con su candidato, como las deja quitar un
            // candidato: mudarlas las atribuiría a uno que nunca las tuvo.
            //
            // Un solo statement: las dos escrituras salen del mismo cálculo de repetidos, y
            // repetirlo arriesga que los dos desempates se separen. Un WITH que escribe corre
            // completo aunque la consulta principal no lea su salida.
            migrationBuilder.Sql("""
                WITH kept AS (
                    SELECT DISTINCT ON (c.quote_id, c.service_supplier_id)
                           c.id, c.quote_id, c.service_supplier_id
                    FROM events.candidate c
                    WHERE c.deleted_at IS NULL
                    ORDER BY c.quote_id, c.service_supplier_id, c.created_at, c.id
                ),
                duplicate AS (
                    SELECT c.id AS duplicate_id, kept.id AS kept_id
                    FROM events.candidate c
                    JOIN kept ON kept.quote_id = c.quote_id
                             AND kept.service_supplier_id = c.service_supplier_id
                    WHERE c.deleted_at IS NULL
                      AND c.id <> kept.id
                ),
                moved AS (
                    UPDATE events.candidate_proposal p
                    SET candidate_id = duplicate.kept_id
                    FROM duplicate
                    WHERE p.candidate_id = duplicate.duplicate_id
                      AND p.deleted_at IS NULL
                    RETURNING p.id
                )
                UPDATE events.candidate c
                SET deleted_at = now(), updated_at = now()
                FROM duplicate
                WHERE c.id = duplicate.duplicate_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_quote_service_supplier",
                schema: "events",
                table: "candidate",
                columns: new[] { "quote_id", "service_supplier_id" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo cae el índice: la fusión de los repetidos no se deshace, porque un candidato con
            // varias propuestas es válido con o sin esta regla y partirlo de nuevo inventaría filas.
            migrationBuilder.DropIndex(
                name: "ux_candidate_quote_service_supplier",
                schema: "events",
                table: "candidate");
        }
    }
}

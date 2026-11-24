using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class OneCityPerEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_roadshow_city_event_id",
                schema: "events",
                table: "roadshow_city");

            // Un Roadshow armado con la ruta de agregar ciudades, ya retirada, puede traer varias
            // vivas, y con ellas el índice único no se crearía. Se conserva la que el acta notarial
            // ya imprimía (menor display_order y, a igual orden, la creada antes) y las demás se dan
            // de baja, así ninguna acta emitida cambia de lugar.
            migrationBuilder.Sql(
                """
                UPDATE events.roadshow_city c
                SET deleted_at = now(),
                    updated_at = now()
                FROM (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY event_id
                               ORDER BY display_order NULLS LAST, created_at) AS position
                    FROM events.roadshow_city
                    WHERE deleted_at IS NULL
                ) ranked
                WHERE c.id = ranked.id
                  AND ranked.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_roadshow_city_event_id",
                schema: "events",
                table: "roadshow_city",
                column: "event_id",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo devuelve el índice anterior: las ciudades que Up dio de baja siguen dadas de baja,
            // porque nada distingue ya cuáles bajó la migración y cuáles una persona.
            migrationBuilder.DropIndex(
                name: "ux_roadshow_city_event_id",
                schema: "events",
                table: "roadshow_city");

            migrationBuilder.CreateIndex(
                name: "ix_roadshow_city_event_id",
                schema: "events",
                table: "roadshow_city",
                column: "event_id");
        }
    }
}

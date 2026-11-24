using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationNamesAProposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La recomendación baja del candidato a la propuesta: un local que cotiza tres salones
            // recomienda uno, y recomendar al local no decía cuál. El alcance del índice no cambia
            // —sigue siendo a lo sumo una por cotización— porque la fila de la propuesta repite el
            // quote_id de su candidato.
            migrationBuilder.AddColumn<bool>(
                name: "is_recommended",
                schema: "events",
                table: "candidate_proposal",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // La más antigua de las que le quedan vivas al candidato recomendado. Hoy todos llevan
            // exactamente una: la varias-por-candidato se habilitó en la migración anterior y
            // ninguna fila llegó a usarla.
            migrationBuilder.Sql("""
                UPDATE events.candidate_proposal p
                SET is_recommended = true
                FROM (
                    SELECT DISTINCT ON (o.candidate_id) o.id
                    FROM events.candidate_proposal o
                    JOIN events.candidate c ON c.id = o.candidate_id
                    WHERE o.deleted_at IS NULL AND c.deleted_at IS NULL AND c.is_recommended
                    ORDER BY o.candidate_id, o.created_at, o.id
                ) first_offer
                WHERE p.id = first_offer.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_proposal_recommended",
                schema: "events",
                table: "candidate_proposal",
                column: "quote_id",
                unique: true,
                filter: "is_recommended AND deleted_at IS NULL");

            migrationBuilder.DropIndex(
                name: "ux_candidate_recommended",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "is_recommended",
                schema: "events",
                table: "candidate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_recommended",
                schema: "events",
                table: "candidate",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Volver atrás sube la marca al candidato dueño de la propuesta recomendada. Se pierde
            // cuál de sus ofertas era: el candidato no tiene dónde guardarlo, y es exactamente la
            // pregunta que esta migración existe para poder responder.
            migrationBuilder.Sql("""
                UPDATE events.candidate c
                SET is_recommended = true
                FROM events.candidate_proposal p
                WHERE p.candidate_id = c.id
                  AND p.is_recommended
                  AND p.deleted_at IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_recommended",
                schema: "events",
                table: "candidate",
                column: "quote_id",
                unique: true,
                filter: "is_recommended AND deleted_at IS NULL");

            migrationBuilder.DropIndex(
                name: "ux_candidate_proposal_recommended",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropColumn(
                name: "is_recommended",
                schema: "events",
                table: "candidate_proposal");
        }
    }
}

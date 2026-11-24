using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalNamesAProposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El ganador baja del candidato a la propuesta, como bajó la recomendación en la
            // migración anterior. Aprobar a un local que cotizó tres salones no decía cuál se
            // contrata, y la respuesta quedaba para semanas después, cuando alguien registraba el
            // monto final. La columna se muda en vez de duplicarse: dos exigirían comprobar que la
            // propuesta es del candidato, y la fila de la propuesta ya trae su candidate_id.
            migrationBuilder.AddColumn<Guid>(
                name: "winner_proposal_id",
                schema: "events",
                table: "quote_decision",
                type: "uuid",
                nullable: true);

            // La oferta del ganador que llegó a firmarse, o la más antigua que le queda viva si
            // ninguna lleva monto todavía. Hoy las dos elecciones coinciden: cada candidato tiene
            // exactamente una propuesta, porque las varias-por-candidato se habilitaron hace dos
            // migraciones y ninguna fila llegó a usarlas.
            migrationBuilder.Sql("""
                UPDATE events.quote_decision d
                SET winner_proposal_id = contracted.id
                FROM (
                    SELECT DISTINCT ON (p.candidate_id) p.candidate_id, p.id
                    FROM events.candidate_proposal p
                    WHERE p.deleted_at IS NULL
                    ORDER BY p.candidate_id, (p.final_amount_amount IS NULL), p.created_at, p.id
                ) contracted
                WHERE d.winner_candidate_id = contracted.candidate_id;
                """);

            migrationBuilder.DropColumn(
                name: "winner_candidate_id",
                schema: "events",
                table: "quote_decision");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "winner_candidate_id",
                schema: "events",
                table: "quote_decision",
                type: "uuid",
                nullable: true);

            // Volver atrás sube el ganador al candidato dueño de la oferta aprobada. Se pierde
            // cuál de sus ofertas se aprobó, que es exactamente la pregunta que esta migración
            // existe para poder responder.
            migrationBuilder.Sql("""
                UPDATE events.quote_decision d
                SET winner_candidate_id = p.candidate_id
                FROM events.candidate_proposal p
                WHERE p.id = d.winner_proposal_id;
                """);

            migrationBuilder.DropColumn(
                name: "winner_proposal_id",
                schema: "events",
                table: "quote_decision");
        }
    }
}

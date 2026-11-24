using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllowSeveralCandidateProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un candidato puede llevar varias propuestas: un local cotizando cada uno de sus
            // salones. El índice único que lo impedía sostenía el contrato viejo, que mostraba el
            // monto sobre el candidato; ahora el candidato devuelve el arreglo entero. En su lugar
            // queda uno no único, que es la consulta que hace toda lectura de detalle.
            // Ninguna fila se mueve: lo que había sigue siendo la única propuesta de su candidato.
            migrationBuilder.DropIndex(
                name: "ux_candidate_proposal_candidate",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_candidate_id",
                schema: "events",
                table: "candidate_proposal",
                column: "candidate_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver atrás falla si algún candidato llegó a tener dos propuestas vivas, y así debe
            // ser: elegir cuál de las dos ofertas se descarta no es decisión de una migración.
            migrationBuilder.DropIndex(
                name: "ix_candidate_proposal_candidate_id",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_proposal_candidate",
                schema: "events",
                table: "candidate_proposal",
                column: "candidate_id",
                unique: true,
                filter: "deleted_at IS NULL");
        }
    }
}

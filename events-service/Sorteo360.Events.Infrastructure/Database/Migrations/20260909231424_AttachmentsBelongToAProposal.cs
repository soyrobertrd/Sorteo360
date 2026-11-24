using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentsBelongToAProposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los adjuntos bajan del candidato a la propuesta, que es el último sitio donde
            // quedaba algo del contrato viejo después de la recomendación y la aprobación. Un
            // local que cotiza tres salones manda tres cotizaciones escritas, y colgadas del
            // candidato ninguna decía a qué precio respalda. La tabla se renombra en vez de
            // rehacerse: las filas que ya están son documentos que alguien subió.
            migrationBuilder.RenameTable(
                name: "candidate_attachment",
                schema: "events",
                newName: "proposal_attachment",
                newSchema: "events");

            // Renombrar la tabla no renombra lo que cuelga de ella, y un nombre viejo en un
            // índice o una constraint es lo que hace ilegible el esquema seis meses después.
            migrationBuilder.Sql("""
                ALTER TABLE events.proposal_attachment
                    RENAME CONSTRAINT "PK_candidate_attachment" TO "PK_proposal_attachment";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE events.proposal_attachment
                    RENAME CONSTRAINT fk_candidate_attachment_stored_file TO fk_proposal_attachment_stored_file;
                """);

            migrationBuilder.RenameIndex(
                name: "ix_candidate_attachment_stored_file_id",
                schema: "events",
                table: "proposal_attachment",
                newName: "ix_proposal_attachment_stored_file_id");

            migrationBuilder.AddColumn<Guid>(
                name: "candidate_proposal_id",
                schema: "events",
                table: "proposal_attachment",
                type: "uuid",
                nullable: true);

            // La oferta más antigua del candidato, prefiriendo una viva. Hoy todas las elecciones
            // coinciden: cada candidato tiene exactamente una propuesta, porque las
            // varias-por-candidato se habilitaron hace tres migraciones y ninguna fila llegó a
            // usarlas. La rama de las eliminadas existe para que un adjunto de un candidato
            // borrado tenga dónde caer en vez de quedarse sin dueño.
            migrationBuilder.Sql("""
                UPDATE events.proposal_attachment a
                SET candidate_proposal_id = oldest.id
                FROM (
                    SELECT DISTINCT ON (p.candidate_id) p.candidate_id, p.id
                    FROM events.candidate_proposal p
                    ORDER BY p.candidate_id, (p.deleted_at IS NOT NULL), p.created_at, p.id
                ) oldest
                WHERE a.candidate_id = oldest.candidate_id;
                """);

            // Si el backfill dejó alguna fila sin dueño, esto falla y la migración entera se
            // deshace. Es lo que se quiere: un adjunto sin propuesta no se puede leer por ninguna
            // ruta, y descubrirlo acá es mejor que descubrirlo cuando alguien lo busca.
            migrationBuilder.AlterColumn<Guid>(
                name: "candidate_proposal_id",
                schema: "events",
                table: "proposal_attachment",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_attachment_candidate",
                schema: "events",
                table: "proposal_attachment");

            migrationBuilder.DropIndex(
                name: "ix_candidate_attachment_candidate_id",
                schema: "events",
                table: "proposal_attachment");

            migrationBuilder.DropColumn(
                name: "candidate_id",
                schema: "events",
                table: "proposal_attachment");

            migrationBuilder.CreateIndex(
                name: "ix_proposal_attachment_candidate_proposal_id",
                schema: "events",
                table: "proposal_attachment",
                column: "candidate_proposal_id");

            migrationBuilder.AddForeignKey(
                name: "fk_proposal_attachment_candidate_proposal",
                schema: "events",
                table: "proposal_attachment",
                column: "candidate_proposal_id",
                principalSchema: "events",
                principalTable: "candidate_proposal",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_proposal_attachment_candidate_proposal",
                schema: "events",
                table: "proposal_attachment");

            migrationBuilder.DropIndex(
                name: "ix_proposal_attachment_candidate_proposal_id",
                schema: "events",
                table: "proposal_attachment");

            migrationBuilder.AddColumn<Guid>(
                name: "candidate_id",
                schema: "events",
                table: "proposal_attachment",
                type: "uuid",
                nullable: true);

            // Volver atrás sube el adjunto al candidato dueño de la oferta. Se pierde a cuál de
            // sus ofertas respaldaba, que es exactamente la pregunta que esta migración existe
            // para poder responder.
            migrationBuilder.Sql("""
                UPDATE events.proposal_attachment a
                SET candidate_id = p.candidate_id
                FROM events.candidate_proposal p
                WHERE p.id = a.candidate_proposal_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "candidate_id",
                schema: "events",
                table: "proposal_attachment",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "candidate_proposal_id",
                schema: "events",
                table: "proposal_attachment");

            // El índice y la FK se crean ya con el nombre viejo, mientras la tabla todavía se
            // llama proposal_attachment; renombrarla es el último paso.
            migrationBuilder.CreateIndex(
                name: "ix_candidate_attachment_candidate_id",
                schema: "events",
                table: "proposal_attachment",
                column: "candidate_id");

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_attachment_candidate",
                schema: "events",
                table: "proposal_attachment",
                column: "candidate_id",
                principalSchema: "events",
                principalTable: "candidate",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.RenameIndex(
                name: "ix_proposal_attachment_stored_file_id",
                schema: "events",
                table: "proposal_attachment",
                newName: "ix_candidate_attachment_stored_file_id");

            migrationBuilder.Sql("""
                ALTER TABLE events.proposal_attachment
                    RENAME CONSTRAINT fk_proposal_attachment_stored_file TO fk_candidate_attachment_stored_file;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE events.proposal_attachment
                    RENAME CONSTRAINT "PK_proposal_attachment" TO "PK_candidate_attachment";
                """);

            migrationBuilder.RenameTable(
                name: "proposal_attachment",
                schema: "events",
                newName: "candidate_attachment",
                newSchema: "events");
        }
    }
}

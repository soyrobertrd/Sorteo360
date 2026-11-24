using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateAttachmentKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El valor por omisión es lo que rellena las filas que ya estaban: todo lo subido
            // hasta acá es la cotización escrita, así que la columna nace correcta sin que haya
            // que recorrer nada.
            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "events",
                table: "candidate_attachment",
                type: "text",
                nullable: false,
                defaultValue: "Quote");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                schema: "events",
                table: "candidate_attachment");
        }
    }
}

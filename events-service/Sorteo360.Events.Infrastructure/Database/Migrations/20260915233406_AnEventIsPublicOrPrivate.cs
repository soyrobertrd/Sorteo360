using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <summary>
    /// El evento declara si se publica, y su idioma deja de ser opcional.
    /// <para>
    /// Las dos columnas se rellenan a mano antes de exigirlas, y ninguna de las dos queda con
    /// <c>DEFAULT</c> en la base: el valor por omisión vive en el borde HTTP, donde una creación
    /// sin visibilidad se vuelve <c>private</c>. Una fila insertada por fuera de la API tiene que
    /// decir cuál quiere.
    /// </para>
    /// </summary>
    public partial class AnEventIsPublicOrPrivate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nace nullable para poder rellenarla; se exige recién abajo. Añadirla NOT NULL con
            // DEFAULT dejaría el default puesto, que es justo lo que no se quiere.
            migrationBuilder.AddColumn<string>(
                name: "visibility",
                schema: "events",
                table: "event",
                type: "text",
                nullable: true);

            // Todo lo que ya existe queda privado. No hay lista de correcciones a mano: el portal
            // todavía no existe, así que ningún evento de ninguna base se anunció nunca por este
            // sistema, y equivocarse cuesta un PATCH por evento.
            migrationBuilder.Sql("UPDATE events.event SET visibility = 'private' WHERE visibility IS NULL;");

            // Un evento sin idioma no tiene plantilla con la que convocar, y con solo el español
            // escrito el día uno, una base llena de nulos dejaría toda campaña sin lanzar.
            migrationBuilder.Sql("UPDATE events.event SET language = 'es' WHERE language IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "visibility",
                schema: "events",
                table: "event",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "language",
                schema: "events",
                table: "event",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_visibility",
                schema: "events",
                table: "event",
                sql: "visibility IN ('public', 'private')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_event_visibility",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "visibility",
                schema: "events",
                table: "event");

            // El idioma vuelve a admitir nulos. Los que se rellenaron con 'es' se quedan así: la
            // migración no sabe cuáles eran nulos antes, y adivinar borraría datos buenos.
            migrationBuilder.AlterColumn<string>(
                name: "language",
                schema: "events",
                table: "event",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}

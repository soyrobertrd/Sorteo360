using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Notifications.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Todavía no hay modelo de dominio, así que no hay DDL de tablas. Lo que esta
            // migración sí deja es el esquema propio del servicio, para que el arranque ya
            // aplique migraciones en todos los entornos y las cuatro tablas del registro
            // lleguen como una migración más y no como el estreno del mecanismo.
            migrationBuilder.EnsureSchema(name: "notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSchema(name: "notifications");
        }
    }
}

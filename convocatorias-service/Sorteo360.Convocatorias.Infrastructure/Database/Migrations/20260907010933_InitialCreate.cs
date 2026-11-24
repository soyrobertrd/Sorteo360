using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sin DDL a propósito: el servicio todavía no tiene modelo de dominio. Lo que
            // esta migración deja es la tabla de historial, así que el arranque ya aplica
            // migraciones en todos los entornos y la primera tabla real llega como una
            // migración más y no como el estreno del mecanismo.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ExtendSupplierCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica', 'Otros')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica', 'Otros')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_category",
                schema: "events",
                table: "service_supplier",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Otros')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_category",
                schema: "events",
                table: "quote",
                sql: "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Otros')");
        }
    }
}

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceSupplierSecondaryCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_capacity_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.AddColumn<List<string>>(
                name: "secondary_categories",
                schema: "events",
                table: "service_supplier",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_capacity_category",
                schema: "events",
                table: "service_supplier",
                sql: "category = 'Venue' OR 'Venue' = ANY (secondary_categories) OR (auditorium_capacity IS NULL AND cocktail_capacity IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_secondary_categories",
                schema: "events",
                table: "service_supplier",
                sql: "secondary_categories <@ ARRAY['Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica']::text[] AND NOT (category = ANY (secondary_categories))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_capacity_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_secondary_categories",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "secondary_categories",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_capacity_category",
                schema: "events",
                table: "service_supplier",
                sql: "category = 'Venue' OR (auditorium_capacity IS NULL AND cocktail_capacity IS NULL)");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueCapacities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "auditorium_capacity",
                schema: "events",
                table: "service_supplier",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cocktail_capacity",
                schema: "events",
                table: "service_supplier",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_capacity_category",
                schema: "events",
                table: "service_supplier",
                sql: "category = 'Venue' OR (auditorium_capacity IS NULL AND cocktail_capacity IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_capacity_positive",
                schema: "events",
                table: "service_supplier",
                sql: "(auditorium_capacity IS NULL OR auditorium_capacity > 0) AND (cocktail_capacity IS NULL OR cocktail_capacity > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_capacity_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_capacity_positive",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "auditorium_capacity",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "cocktail_capacity",
                schema: "events",
                table: "service_supplier");
        }
    }
}

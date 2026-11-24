using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierIntakeLinkNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "created_by_name",
                schema: "convocatorias",
                table: "supplier_intake_link",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deactivated_by",
                schema: "convocatorias",
                table: "supplier_intake_link",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deactivated_by_name",
                schema: "convocatorias",
                table: "supplier_intake_link",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_name",
                schema: "convocatorias",
                table: "supplier_intake_link");

            migrationBuilder.DropColumn(
                name: "deactivated_by",
                schema: "convocatorias",
                table: "supplier_intake_link");

            migrationBuilder.DropColumn(
                name: "deactivated_by_name",
                schema: "convocatorias",
                table: "supplier_intake_link");
        }
    }
}

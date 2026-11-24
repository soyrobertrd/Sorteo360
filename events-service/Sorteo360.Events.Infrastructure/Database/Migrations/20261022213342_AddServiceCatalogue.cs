using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_catalogue_item",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    reference_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reference_price_currency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_catalogue_item", x => x.id);
                    table.CheckConstraint("ck_service_catalogue_item_reference_price_currency", "reference_price_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_service_catalogue_item_reference_price_positive", "reference_price_amount > 0");
                    table.ForeignKey(
                        name: "fk_service_catalogue_item_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_catalogue_item_service_supplier_id",
                schema: "events",
                table: "service_catalogue_item",
                column: "service_supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_catalogue_item",
                schema: "events");
        }
    }
}

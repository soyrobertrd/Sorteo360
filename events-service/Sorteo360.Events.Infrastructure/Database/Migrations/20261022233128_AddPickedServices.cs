using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPickedServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "picked_service",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    service_catalogue_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price_currency = table.Column<string>(type: "text", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    discount_currency = table.Column<string>(type: "text", nullable: true),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    tax_currency = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_picked_service", x => x.id);
                    table.CheckConstraint("ck_picked_service_discount", "discount_amount IS NULL OR (discount_amount >= 0 AND discount_currency IS NOT NULL)");
                    table.CheckConstraint("ck_picked_service_discount_currency", "discount_currency IS NULL OR discount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_picked_service_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_picked_service_tax", "tax_amount IS NULL OR (tax_amount >= 0 AND tax_currency IS NOT NULL)");
                    table.CheckConstraint("ck_picked_service_tax_currency", "tax_currency IS NULL OR tax_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_picked_service_unit_price_currency", "unit_price_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_picked_service_unit_price_positive", "unit_price_amount > 0");
                    table.ForeignKey(
                        name: "fk_picked_service_candidate_proposal",
                        column: x => x.candidate_proposal_id,
                        principalSchema: "events",
                        principalTable: "candidate_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_picked_service_candidate_proposal_id",
                schema: "events",
                table: "picked_service",
                column: "candidate_proposal_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "picked_service",
                schema: "events");
        }
    }
}

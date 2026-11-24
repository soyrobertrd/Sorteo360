using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceSupplierEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_supplier_evaluation",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_quality = table.Column<int>(type: "integer", nullable: false),
                    punctuality = table.Column<int>(type: "integer", nullable: false),
                    value_for_money = table.Column<int>(type: "integer", nullable: false),
                    communication = table.Column<int>(type: "integer", nullable: false),
                    would_hire_again = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier_evaluation", x => x.id);
                    table.CheckConstraint("ck_service_supplier_evaluation_scores", "service_quality BETWEEN 1 AND 5 AND punctuality BETWEEN 1 AND 5 AND value_for_money BETWEEN 1 AND 5 AND communication BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_service_supplier_evaluation_event",
                        column: x => x.event_id,
                        principalSchema: "events",
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_supplier_evaluation_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_evaluation_event_id",
                schema: "events",
                table: "service_supplier_evaluation",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ux_service_supplier_evaluation_service_supplier_event",
                schema: "events",
                table: "service_supplier_evaluation",
                columns: new[] { "service_supplier_id", "event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_supplier_evaluation",
                schema: "events");
        }
    }
}

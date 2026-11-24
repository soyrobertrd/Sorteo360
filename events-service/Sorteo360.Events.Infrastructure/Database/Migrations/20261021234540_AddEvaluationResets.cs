using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationResets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_supplier_evaluation_reset",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reset_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reset_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier_evaluation_reset", x => x.id);
                    table.CheckConstraint("ck_service_supplier_evaluation_reset_reason", "btrim(reason) <> ''");
                    table.ForeignKey(
                        name: "fk_service_supplier_evaluation_reset_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_evaluation_reset_service_supplier_reset_at",
                schema: "events",
                table: "service_supplier_evaluation_reset",
                columns: new[] { "service_supplier_id", "reset_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_supplier_evaluation_reset",
                schema: "events");
        }
    }
}

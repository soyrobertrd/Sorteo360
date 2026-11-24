using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierIntakeLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_intake_link",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    opt_office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_intake_link", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_intake_send",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opt_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_intake_send", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_intake_send_supplier_intake_link_link_id",
                        column: x => x.link_id,
                        principalSchema: "convocatorias",
                        principalTable: "supplier_intake_link",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_intake_link_active_office",
                schema: "convocatorias",
                table: "supplier_intake_link",
                column: "opt_office_id",
                unique: true,
                filter: "deactivated_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_supplier_intake_link_token_hash",
                schema: "convocatorias",
                table: "supplier_intake_link",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_intake_send_link_created_at",
                schema: "convocatorias",
                table: "supplier_intake_send",
                columns: new[] { "link_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_intake_send",
                schema: "convocatorias");

            migrationBuilder.DropTable(
                name: "supplier_intake_link",
                schema: "convocatorias");
        }
    }
}

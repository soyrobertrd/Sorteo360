using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceSupplierUnhireableReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unhireable_reason",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_unhireable_reason",
                schema: "events",
                table: "service_supplier",
                sql: "is_hireable = false OR unhireable_reason IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_unhireable_reason",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "unhireable_reason",
                schema: "events",
                table: "service_supplier");
        }
    }
}

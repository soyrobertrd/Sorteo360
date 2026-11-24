using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_photo_ref",
                schema: "events",
                table: "service_supplier_photo");

            migrationBuilder.AddColumn<Guid>(
                name: "stored_file_id",
                schema: "events",
                table: "service_supplier_photo",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stored_file",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "text", nullable: false),
                    missing_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_file", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_photo_stored_file_id",
                schema: "events",
                table: "service_supplier_photo",
                column: "stored_file_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_photo_ref",
                schema: "events",
                table: "service_supplier_photo",
                sql: "file_ref_id IS NOT NULL OR url IS NOT NULL OR stored_file_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_stored_file_provider_storage_key",
                schema: "events",
                table: "stored_file",
                columns: new[] { "provider", "storage_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_service_supplier_photo_stored_file",
                schema: "events",
                table: "service_supplier_photo",
                column: "stored_file_id",
                principalSchema: "events",
                principalTable: "stored_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_service_supplier_photo_stored_file",
                schema: "events",
                table: "service_supplier_photo");

            migrationBuilder.DropTable(
                name: "stored_file",
                schema: "events");

            migrationBuilder.DropIndex(
                name: "ix_service_supplier_photo_stored_file_id",
                schema: "events",
                table: "service_supplier_photo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_photo_ref",
                schema: "events",
                table: "service_supplier_photo");

            migrationBuilder.DropColumn(
                name: "stored_file_id",
                schema: "events",
                table: "service_supplier_photo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_photo_ref",
                schema: "events",
                table: "service_supplier_photo",
                sql: "file_ref_id IS NOT NULL OR url IS NOT NULL");
        }
    }
}

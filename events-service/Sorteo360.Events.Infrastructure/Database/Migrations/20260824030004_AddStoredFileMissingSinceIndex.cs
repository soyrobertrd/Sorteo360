using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFileMissingSinceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_stored_file_missing_since",
                schema: "events",
                table: "stored_file",
                column: "missing_since",
                filter: "missing_since IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stored_file_missing_since",
                schema: "events",
                table: "stored_file");
        }
    }
}

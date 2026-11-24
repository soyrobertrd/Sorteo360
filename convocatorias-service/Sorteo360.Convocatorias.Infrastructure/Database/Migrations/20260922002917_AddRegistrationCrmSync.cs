using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationCrmSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "crm_contact_id",
                schema: "convocatorias",
                table: "registration",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "crm_sync_failed_at",
                schema: "convocatorias",
                table: "registration",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "crm_contact_id",
                schema: "convocatorias",
                table: "registration");

            migrationBuilder.DropColumn(
                name: "crm_sync_failed_at",
                schema: "convocatorias",
                table: "registration");
        }
    }
}

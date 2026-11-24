using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_in_at",
                schema: "convocatorias",
                table: "registration",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "checked_in_by_id",
                schema: "convocatorias",
                table: "registration",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_registration_contact_checked_in",
                schema: "convocatorias",
                table: "registration",
                column: "crm_contact_id",
                filter: "checked_in_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_registration_event_checked_in",
                schema: "convocatorias",
                table: "registration",
                column: "event_id",
                filter: "checked_in_at IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_registration_check_in_pair",
                schema: "convocatorias",
                table: "registration",
                sql: "(checked_in_at IS NULL) = (checked_in_by_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_registration_contact_checked_in",
                schema: "convocatorias",
                table: "registration");

            migrationBuilder.DropIndex(
                name: "ix_registration_event_checked_in",
                schema: "convocatorias",
                table: "registration");

            migrationBuilder.DropCheckConstraint(
                name: "ck_registration_check_in_pair",
                schema: "convocatorias",
                table: "registration");

            migrationBuilder.DropColumn(
                name: "checked_in_at",
                schema: "convocatorias",
                table: "registration");

            migrationBuilder.DropColumn(
                name: "checked_in_by_id",
                schema: "convocatorias",
                table: "registration");
        }
    }
}

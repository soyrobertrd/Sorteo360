using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropEventOptColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_event_opt_office",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "opt_contact_id",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "opt_office_id",
                schema: "events",
                table: "event");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "opt_contact_id",
                schema: "events",
                table: "event",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "opt_office_id",
                schema: "events",
                table: "event",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_event_opt_office",
                schema: "events",
                table: "event",
                column: "opt_office_id",
                filter: "deleted_at IS NULL");
        }
    }
}

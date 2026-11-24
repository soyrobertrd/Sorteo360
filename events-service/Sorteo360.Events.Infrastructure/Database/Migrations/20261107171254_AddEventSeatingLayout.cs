using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEventSeatingLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "seating_layout",
                schema: "events",
                table: "event",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_seating_layout",
                schema: "events",
                table: "event",
                sql: "seating_layout IS NULL OR seating_layout IN ('Banquet', 'Auditorium', 'Conference', 'Cocktail', 'UShape', 'Classroom')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_event_seating_layout",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "seating_layout",
                schema: "events",
                table: "event");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignEventValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "event_dates",
                schema: "convocatorias",
                table: "campaign",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "event_location",
                schema: "convocatorias",
                table: "campaign",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "event_name",
                schema: "convocatorias",
                table: "campaign",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_campaign_event_values_are_whole",
                schema: "convocatorias",
                table: "campaign",
                sql: "(event_name IS NULL) = (event_location IS NULL) AND (event_name IS NULL) = (event_dates IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_campaign_event_values_are_whole",
                schema: "convocatorias",
                table: "campaign");

            migrationBuilder.DropColumn(
                name: "event_dates",
                schema: "convocatorias",
                table: "campaign");

            migrationBuilder.DropColumn(
                name: "event_location",
                schema: "convocatorias",
                table: "campaign");

            migrationBuilder.DropColumn(
                name: "event_name",
                schema: "convocatorias",
                table: "campaign");
        }
    }
}

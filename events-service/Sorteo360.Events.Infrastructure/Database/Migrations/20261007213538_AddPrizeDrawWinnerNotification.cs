using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeDrawWinnerNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "winner_notification_id",
                schema: "events",
                table: "prize_draw",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "winner_notification_status",
                schema: "events",
                table: "prize_draw",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_prize_draw_winner_notification_status",
                schema: "events",
                table: "prize_draw",
                sql: "winner_notification_status IS NULL OR winner_notification_status IN ('failed', 'no_recipient', 'sent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_prize_draw_winner_notification_status",
                schema: "events",
                table: "prize_draw");

            migrationBuilder.DropColumn(
                name: "winner_notification_id",
                schema: "events",
                table: "prize_draw");

            migrationBuilder.DropColumn(
                name: "winner_notification_status",
                schema: "events",
                table: "prize_draw");
        }
    }
}

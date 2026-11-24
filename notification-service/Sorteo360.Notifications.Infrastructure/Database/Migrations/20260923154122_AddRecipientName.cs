using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Notifications.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "notifications",
                table: "notification_recipient",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                schema: "notifications",
                table: "notification_recipient");
        }
    }
}

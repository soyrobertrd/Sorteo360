using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeImageStoredFileId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "image_stored_file_id",
                schema: "events",
                table: "prize",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_prize_image_stored_file_id",
                schema: "events",
                table: "prize",
                column: "image_stored_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_prize_image_stored_file",
                schema: "events",
                table: "prize",
                column: "image_stored_file_id",
                principalSchema: "events",
                principalTable: "stored_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_prize_image_stored_file",
                schema: "events",
                table: "prize");

            migrationBuilder.DropIndex(
                name: "ix_prize_image_stored_file_id",
                schema: "events",
                table: "prize");

            migrationBuilder.DropColumn(
                name: "image_stored_file_id",
                schema: "events",
                table: "prize");
        }
    }
}

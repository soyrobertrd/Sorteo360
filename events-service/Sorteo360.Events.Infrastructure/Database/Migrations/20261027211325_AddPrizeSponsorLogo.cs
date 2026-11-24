using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeSponsorLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sponsor_logo_file_id",
                schema: "events",
                table: "prize",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_prize_sponsor_logo_file_id",
                schema: "events",
                table: "prize",
                column: "sponsor_logo_file_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_prize_sponsor_logo_needs_company",
                schema: "events",
                table: "prize",
                sql: "sponsor_logo_file_id IS NULL OR sponsor_company_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_prize_sponsor_logo_stored_file",
                schema: "events",
                table: "prize",
                column: "sponsor_logo_file_id",
                principalSchema: "events",
                principalTable: "stored_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_prize_sponsor_logo_stored_file",
                schema: "events",
                table: "prize");

            migrationBuilder.DropIndex(
                name: "ix_prize_sponsor_logo_file_id",
                schema: "events",
                table: "prize");

            migrationBuilder.DropCheckConstraint(
                name: "ck_prize_sponsor_logo_needs_company",
                schema: "events",
                table: "prize");

            migrationBuilder.DropColumn(
                name: "sponsor_logo_file_id",
                schema: "events",
                table: "prize");
        }
    }
}

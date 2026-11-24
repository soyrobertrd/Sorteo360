using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeSponsor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sponsor_company_id",
                schema: "events",
                table: "prize",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sponsor_contact_id",
                schema: "events",
                table: "prize",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_prize_sponsor_contact_needs_company",
                schema: "events",
                table: "prize",
                sql: "sponsor_contact_id IS NULL OR sponsor_company_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_prize_sponsor_contact_needs_company",
                schema: "events",
                table: "prize");

            migrationBuilder.DropColumn(
                name: "sponsor_company_id",
                schema: "events",
                table: "prize");

            migrationBuilder.DropColumn(
                name: "sponsor_contact_id",
                schema: "events",
                table: "prize");
        }
    }
}

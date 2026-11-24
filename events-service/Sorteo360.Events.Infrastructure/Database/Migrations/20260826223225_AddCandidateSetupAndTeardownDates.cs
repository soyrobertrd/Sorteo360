using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateSetupAndTeardownDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "setup_date",
                schema: "events",
                table: "candidate",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "teardown_date",
                schema: "events",
                table: "candidate",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_setup_before_teardown",
                schema: "events",
                table: "candidate",
                sql: "setup_date IS NULL OR teardown_date IS NULL OR teardown_date >= setup_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_setup_before_teardown",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "setup_date",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "teardown_date",
                schema: "events",
                table: "candidate");
        }
    }
}

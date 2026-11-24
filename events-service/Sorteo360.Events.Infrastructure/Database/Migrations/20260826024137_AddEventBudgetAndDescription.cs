using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEventBudgetAndDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "events",
                table: "event",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "estimated_budget_amount",
                schema: "events",
                table: "event",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estimated_budget_currency",
                schema: "events",
                table: "event",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_estimated_budget",
                schema: "events",
                table: "event",
                sql: "estimated_budget_amount IS NULL OR estimated_budget_currency IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_estimated_budget_amount",
                schema: "events",
                table: "event",
                sql: "estimated_budget_amount IS NULL OR estimated_budget_amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_estimated_budget_currency",
                schema: "events",
                table: "event",
                sql: "estimated_budget_currency IS NULL OR estimated_budget_currency IN ('USD', 'EUR')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_event_estimated_budget",
                schema: "events",
                table: "event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_event_estimated_budget_amount",
                schema: "events",
                table: "event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_event_estimated_budget_currency",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "estimated_budget_amount",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "estimated_budget_currency",
                schema: "events",
                table: "event");
        }
    }
}

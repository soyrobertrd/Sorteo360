using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayFieldsAndPlanRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "events",
                table: "raffle_participant",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "events",
                table: "raffle_participant",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                schema: "events",
                table: "raffle_participant",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_detail_mode",
                schema: "events",
                table: "raffle",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "department");

            migrationBuilder.AddColumn<string>(
                name: "display_identifier_mode",
                schema: "events",
                table: "raffle",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ticket_number");

            migrationBuilder.AddColumn<string>(
                name: "display_name_mode",
                schema: "events",
                table: "raffle",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "full_name");

            migrationBuilder.CreateTable(
                name: "account",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "free"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Active"),
                    owner_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    plan_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    plan_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expiry_warning_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account", x => x.id);
                    table.CheckConstraint("ck_account_plan", "plan IN ('free', 'basic', 'pro', 'advanced')");
                    table.CheckConstraint("ck_account_status", "status IN ('PendingActivation', 'Active', 'Suspended')");
                });

            migrationBuilder.CreateTable(
                name: "plan_request",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    transfer_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    requested_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    paid_period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_request", x => x.id);
                    table.CheckConstraint("ck_plan_request_plan", "requested_plan IN ('basic', 'pro', 'advanced')");
                    table.CheckConstraint("ck_plan_request_status", "status IN ('Pending', 'Approved', 'Rejected', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_plan_request_account",
                        column: x => x.account_id,
                        principalSchema: "events",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_plan_expires_at",
                schema: "events",
                table: "account",
                column: "plan_expires_at",
                filter: "plan_expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_plan_request_status_requested_at",
                schema: "events",
                table: "plan_request",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ux_plan_request_account_pending",
                schema: "events",
                table: "plan_request",
                column: "account_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.Sql("""
                INSERT INTO events.account (id, name, plan, status, plan_started_at, created_at, updated_at)
                VALUES ('00000000-0000-0000-0000-000000000001', 'Banreservas', 'advanced', 'Active', now(), now(), now())
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_request",
                schema: "events");

            migrationBuilder.DropTable(
                name: "account",
                schema: "events");

            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "events",
                table: "raffle_participant");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "events",
                table: "raffle_participant");

            migrationBuilder.DropColumn(
                name: "location",
                schema: "events",
                table: "raffle_participant");

            migrationBuilder.DropColumn(
                name: "display_detail_mode",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "display_identifier_mode",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "display_name_mode",
                schema: "events",
                table: "raffle");
        }
    }
}

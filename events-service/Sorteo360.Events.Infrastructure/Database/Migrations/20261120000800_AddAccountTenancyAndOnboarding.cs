using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTenancyAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "account_id",
                schema: "events",
                table: "event",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "account_id",
                schema: "events",
                table: "branding_setting",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateTable(
                name: "account_activation",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email_normalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    requested_plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    billing_period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "monthly"),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resend_count_today = table.Column<int>(type: "integer", nullable: false),
                    resend_day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_activation", x => x.id);
                    table.CheckConstraint("ck_account_activation_billing_period", "billing_period IN ('monthly', 'annual')");
                    table.ForeignKey(
                        name: "fk_account_activation_account",
                        column: x => x.account_id,
                        principalSchema: "events",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_monthly_usage",
                schema: "events",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    raffles_created = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_monthly_usage", x => new { x.account_id, x.year_month });
                    table.ForeignKey(
                        name: "fk_account_monthly_usage_account",
                        column: x => x.account_id,
                        principalSchema: "events",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_account_id",
                schema: "events",
                table: "event",
                column: "account_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_account_activation_account_id",
                schema: "events",
                table: "account_activation",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_activation_email",
                schema: "events",
                table: "account_activation",
                column: "email_normalized");

            migrationBuilder.CreateIndex(
                name: "ux_account_activation_token_hash",
                schema: "events",
                table: "account_activation",
                column: "token_hash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_event_account",
                schema: "events",
                table: "event",
                column: "account_id",
                principalSchema: "events",
                principalTable: "account",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_event_account",
                schema: "events",
                table: "event");

            migrationBuilder.DropTable(
                name: "account_activation",
                schema: "events");

            migrationBuilder.DropTable(
                name: "account_monthly_usage",
                schema: "events");

            migrationBuilder.DropIndex(
                name: "ix_event_account_id",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "account_id",
                schema: "events",
                table: "event");

            migrationBuilder.DropColumn(
                name: "account_id",
                schema: "events",
                table: "branding_setting");
        }
    }
}

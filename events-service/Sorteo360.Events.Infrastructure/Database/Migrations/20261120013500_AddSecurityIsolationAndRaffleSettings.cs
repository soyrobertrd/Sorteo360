using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityIsolationAndRaffleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // S4: aislamiento por cuenta en archivos almacenados (firmas, actas, imágenes).
            migrationBuilder.AddColumn<Guid>(
                name: "account_id",
                schema: "events",
                table: "stored_file",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "ix_stored_file_account_id",
                schema: "events",
                table: "stored_file",
                column: "account_id");

            // R5: clave de webhook por evento (y backfill para eventos existentes).
            migrationBuilder.AddColumn<string>(
                name: "webhook_secret",
                schema: "events",
                table: "event",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE events.event
                SET webhook_secret = md5(id::text || clock_timestamp()::text || random()::text)
                                  || md5(random()::text || id::text)
                WHERE webhook_secret IS NULL;
                """);

            // R2 / A35: participar sin estar presente (requires_check_in, por omisión true).
            migrationBuilder.AddColumn<bool>(
                name: "requires_check_in",
                schema: "events",
                table: "raffle",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // A33: ajustes del tablero de ganadores y visual_style 'winner_board'.
            migrationBuilder.AddColumn<int>(
                name: "board_batch_size",
                schema: "events",
                table: "raffle",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "board_identifier",
                schema: "events",
                table: "raffle",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "board_subtitle",
                schema: "events",
                table: "raffle",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "board_title",
                schema: "events",
                table: "raffle",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle");

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle",
                sql: "visual_style IN ('roulette', 'sequential_names', 'winner_board')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_board_identifier",
                schema: "events",
                table: "raffle",
                sql: "board_identifier IS NULL OR board_identifier IN ('employee_code', 'ticket')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_board_batch_size",
                schema: "events",
                table: "raffle",
                sql: "board_batch_size IS NULL OR (board_batch_size BETWEEN 1 AND 10)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_board_batch_size",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_board_identifier",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle");

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_visual_style",
                schema: "events",
                table: "raffle",
                sql: "visual_style IN ('roulette', 'sequential_names')");

            migrationBuilder.DropColumn(
                name: "board_title",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "board_subtitle",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "board_identifier",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "board_batch_size",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "requires_check_in",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "webhook_secret",
                schema: "events",
                table: "event");

            migrationBuilder.DropIndex(
                name: "ix_stored_file_account_id",
                schema: "events",
                table: "stored_file");

            migrationBuilder.DropColumn(
                name: "account_id",
                schema: "events",
                table: "stored_file");
        }
    }
}


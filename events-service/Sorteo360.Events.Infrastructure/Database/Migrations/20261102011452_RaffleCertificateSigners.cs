using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RaffleCertificateSigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "certificate_signer_one_name",
                schema: "events",
                table: "raffle",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_signer_one_title",
                schema: "events",
                table: "raffle",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_signer_two_name",
                schema: "events",
                table: "raffle",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_signer_two_title",
                schema: "events",
                table: "raffle",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_certificate_signer_one",
                schema: "events",
                table: "raffle",
                sql: "(certificate_signer_one_name IS NULL) = (certificate_signer_one_title IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_certificate_signer_two",
                schema: "events",
                table: "raffle",
                sql: "(certificate_signer_two_name IS NULL) = (certificate_signer_two_title IS NULL) AND (certificate_signer_two_name IS NULL OR certificate_signer_one_name IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_certificate_signer_one",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_certificate_signer_two",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "certificate_signer_one_name",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "certificate_signer_one_title",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "certificate_signer_two_name",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "certificate_signer_two_title",
                schema: "events",
                table: "raffle");
        }
    }
}

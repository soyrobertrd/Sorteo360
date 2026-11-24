using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaffleCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                schema: "events",
                table: "raffle",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completed_by_id",
                schema: "events",
                table: "raffle",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_instructions",
                schema: "events",
                table: "raffle",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notarial_record",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raffle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    generated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notarial_record", x => x.id);
                    table.CheckConstraint("ck_notarial_record_version", "version >= 1");
                    table.ForeignKey(
                        name: "fk_notarial_record_raffle",
                        column: x => x.raffle_id,
                        principalSchema: "events",
                        principalTable: "raffle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notarial_record_stored_file",
                        column: x => x.stored_file_id,
                        principalSchema: "events",
                        principalTable: "stored_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_raffle_completed",
                schema: "events",
                table: "raffle",
                sql: "(completed_at IS NULL) = (completed_by_id IS NULL) AND (completed_at IS NOT NULL) = (state = 'completed')");

            migrationBuilder.CreateIndex(
                name: "ix_notarial_record_stored_file_id",
                schema: "events",
                table: "notarial_record",
                column: "stored_file_id");

            migrationBuilder.CreateIndex(
                name: "ux_notarial_record_raffle_id_version",
                schema: "events",
                table: "notarial_record",
                columns: new[] { "raffle_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notarial_record",
                schema: "events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_raffle_completed",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "completed_by_id",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "pickup_instructions",
                schema: "events",
                table: "raffle");
        }
    }
}

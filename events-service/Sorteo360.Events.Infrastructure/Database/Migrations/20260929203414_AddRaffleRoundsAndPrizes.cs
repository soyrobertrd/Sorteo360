using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaffleRoundsAndPrizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "raffle_round",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raffle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raffle_round", x => x.id);
                    table.CheckConstraint("ck_raffle_round_number", "number >= 1");
                    table.ForeignKey(
                        name: "fk_raffle_round_raffle",
                        column: x => x.raffle_id,
                        principalSchema: "events",
                        principalTable: "raffle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prize",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prize", x => x.id);
                    table.ForeignKey(
                        name: "fk_prize_raffle_round",
                        column: x => x.round_id,
                        principalSchema: "events",
                        principalTable: "raffle_round",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prize_round_id",
                schema: "events",
                table: "prize",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "ix_raffle_round_raffle_id",
                schema: "events",
                table: "raffle_round",
                column: "raffle_id");

            // Escrito a mano porque EF no sabe declarar un UNIQUE DEFERRABLE. Tiene que serlo: al
            // eliminar una ronda, las de detrás bajan un puesto en un solo UPDATE, y un único
            // inmediato se comprueba fila por fila y chocaría a mitad de la sentencia. Diferido, se
            // comprueba al confirmar, cuando los números ya son 1..n sin huecos.
            migrationBuilder.Sql(
                "ALTER TABLE events.raffle_round ADD CONSTRAINT ux_raffle_round_raffle_id_number " +
                "UNIQUE (raffle_id, number) DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE events.raffle_round DROP CONSTRAINT ux_raffle_round_raffle_id_number;");

            migrationBuilder.DropTable(
                name: "prize",
                schema: "events");

            migrationBuilder.DropTable(
                name: "raffle_round",
                schema: "events");
        }
    }
}

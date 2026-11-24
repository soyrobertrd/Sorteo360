using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Cada sorteo de un premio guarda las dos reglas bajo las que se sorteó, porque desde PGTI-275
    /// pueden cambiar con el sorteo en curso (spec 02 §1.2).
    /// <para>
    /// Las filas que ya existían toman las reglas actuales de su sorteo. Es exacto y no una
    /// aproximación: hasta esta migración las reglas no podían cambiar después de <c>start</c>, y no
    /// hay sorteos de premio antes de <c>start</c>.
    /// </para>
    /// </summary>
    public partial class RecordPrizeDrawRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "one_prize_per_company",
                schema: "events",
                table: "prize_draw",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "one_prize_per_person",
                schema: "events",
                table: "prize_draw",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE events.prize_draw d
                SET one_prize_per_person = r.one_prize_per_person,
                    one_prize_per_company = r.one_prize_per_company
                FROM events.prize p
                JOIN events.raffle_round rr ON rr.id = p.round_id
                JOIN events.raffle r ON r.id = rr.raffle_id
                WHERE p.id = d.prize_id;
                """);

            // El default era solo para poder llegar a NOT NULL con filas ya escritas. Se quita
            // enseguida: el modelo no declara ninguno, y un sorteo que no dijera bajo qué reglas salió
            // tiene que fallar en lugar de quedar con un "no" que nadie eligió.
            migrationBuilder.Sql(
                """
                ALTER TABLE events.prize_draw
                    ALTER COLUMN one_prize_per_person DROP DEFAULT,
                    ALTER COLUMN one_prize_per_company DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "one_prize_per_company",
                schema: "events",
                table: "prize_draw");

            migrationBuilder.DropColumn(
                name: "one_prize_per_person",
                schema: "events",
                table: "prize_draw");
        }
    }
}

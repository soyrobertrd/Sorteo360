using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_proposal",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_text = table.Column<string>(type: "text", nullable: true),
                    setup_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    teardown_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estimated_amount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    estimated_amount_currency = table.Column<string>(type: "text", nullable: false),
                    final_amount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    final_amount_currency = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_proposal", x => x.id);
                    table.CheckConstraint("ck_candidate_proposal_estimated_amount_currency", "estimated_amount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_candidate_proposal_final_amount", "final_amount_amount IS NULL OR final_amount_currency IS NOT NULL");
                    table.CheckConstraint("ck_candidate_proposal_final_amount_currency", "final_amount_currency IS NULL OR final_amount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_candidate_proposal_setup_before_teardown", "setup_date IS NULL OR teardown_date IS NULL OR teardown_date >= setup_date");
                    table.ForeignKey(
                        name: "fk_candidate_proposal_candidate",
                        column: x => x.candidate_id,
                        principalSchema: "events",
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidate_proposal_quote",
                        column: x => x.quote_id,
                        principalSchema: "events",
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_quote_id",
                schema: "events",
                table: "candidate_proposal",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_proposal_candidate",
                schema: "events",
                table: "candidate_proposal",
                column: "candidate_id",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_proposal_final_amount",
                schema: "events",
                table: "candidate_proposal",
                column: "quote_id",
                unique: true,
                filter: "final_amount_amount IS NOT NULL AND deleted_at IS NULL");

            // Cada candidato existente pasa a tener exactamente una propuesta con lo que ya
            // llevaba: el índice único parcial de candidate_proposal cuenta con eso. Los
            // candidatos borrados copian su deleted_at, así que sus propuestas nacen borradas y
            // no ocupan el lugar único de un candidato vivo.
            migrationBuilder.Sql("""
                INSERT INTO events.candidate_proposal (
                    id, candidate_id, quote_id, quote_text, setup_date, teardown_date,
                    estimated_amount_amount, estimated_amount_currency,
                    final_amount_amount, final_amount_currency,
                    created_at, updated_at, created_by_id, updated_by_id, deleted_at)
                SELECT
                    gen_random_uuid(), c.id, c.quote_id, c.quote_text, c.setup_date, c.teardown_date,
                    c.estimated_amount_amount, c.estimated_amount_currency,
                    c.final_amount_amount, c.final_amount_currency,
                    c.created_at, c.updated_at, c.created_by_id, c.updated_by_id, c.deleted_at
                FROM events.candidate c;
                """);

            migrationBuilder.DropIndex(
                name: "ux_candidate_final_amount",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_estimated_amount_currency",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_final_amount",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_final_amount_currency",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_setup_before_teardown",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "estimated_amount_amount",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "estimated_amount_currency",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "final_amount_amount",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "final_amount_currency",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "quote_text",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "estimated_amount_amount",
                schema: "events",
                table: "candidate",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "estimated_amount_currency",
                schema: "events",
                table: "candidate",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "final_amount_amount",
                schema: "events",
                table: "candidate",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "final_amount_currency",
                schema: "events",
                table: "candidate",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quote_text",
                schema: "events",
                table: "candidate",
                type: "text",
                nullable: true);

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

            // Se devuelven los valores a candidate antes de recrear índice y restricciones, para
            // que las encuentren con los datos ya puestos. Si un candidato llegara a tener más de
            // una propuesta, vuelve la viva, y entre varias la más antigua: una borrada nunca gana,
            // porque sus montos ya no son los que el candidato mostraba.
            migrationBuilder.Sql("""
                UPDATE events.candidate c
                SET quote_text = p.quote_text,
                    setup_date = p.setup_date,
                    teardown_date = p.teardown_date,
                    estimated_amount_amount = p.estimated_amount_amount,
                    estimated_amount_currency = p.estimated_amount_currency,
                    final_amount_amount = p.final_amount_amount,
                    final_amount_currency = p.final_amount_currency
                FROM (
                    SELECT DISTINCT ON (candidate_id) *
                    FROM events.candidate_proposal
                    ORDER BY candidate_id, (deleted_at IS NULL) DESC, created_at
                ) p
                WHERE p.candidate_id = c.id;
                """);

            migrationBuilder.DropTable(
                name: "candidate_proposal",
                schema: "events");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_final_amount",
                schema: "events",
                table: "candidate",
                column: "quote_id",
                unique: true,
                filter: "final_amount_amount IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_estimated_amount_currency",
                schema: "events",
                table: "candidate",
                sql: "estimated_amount_currency IN ('USD', 'EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_final_amount",
                schema: "events",
                table: "candidate",
                sql: "final_amount_amount IS NULL OR final_amount_currency IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_final_amount_currency",
                schema: "events",
                table: "candidate",
                sql: "final_amount_currency IS NULL OR final_amount_currency IN ('USD', 'EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_setup_before_teardown",
                schema: "events",
                table: "candidate",
                sql: "setup_date IS NULL OR teardown_date IS NULL OR teardown_date >= setup_date");
        }
    }
}

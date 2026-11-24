using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProposalPendingFinalAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "pending_final_amount_amount",
                schema: "events",
                table: "candidate_proposal",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_final_amount_currency",
                schema: "events",
                table: "candidate_proposal",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pending_final_amount_recorded_at",
                schema: "events",
                table: "candidate_proposal",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pending_final_amount_recorded_by_id",
                schema: "events",
                table: "candidate_proposal",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_proposal_pending_final_amount",
                schema: "events",
                table: "candidate_proposal",
                sql: "(pending_final_amount_amount IS NULL AND pending_final_amount_currency IS NULL AND pending_final_amount_recorded_by_id IS NULL AND pending_final_amount_recorded_at IS NULL) OR (pending_final_amount_amount IS NOT NULL AND pending_final_amount_currency IS NOT NULL AND pending_final_amount_recorded_by_id IS NOT NULL AND pending_final_amount_recorded_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_proposal_pending_final_amount_currency",
                schema: "events",
                table: "candidate_proposal",
                sql: "pending_final_amount_currency IS NULL OR pending_final_amount_currency IN ('USD', 'EUR')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_proposal_pending_final_amount",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_proposal_pending_final_amount_currency",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropColumn(
                name: "pending_final_amount_amount",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropColumn(
                name: "pending_final_amount_currency",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropColumn(
                name: "pending_final_amount_recorded_at",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropColumn(
                name: "pending_final_amount_recorded_by_id",
                schema: "events",
                table: "candidate_proposal");
        }
    }
}

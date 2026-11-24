using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProposalFinalQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pending_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal",
                column: "final_amount_quote_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_pending_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal",
                column: "pending_final_amount_quote_attachment_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_proposal_final_amount_quote",
                schema: "events",
                table: "candidate_proposal",
                sql: "final_amount_quote_attachment_id IS NULL OR final_amount_amount IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_proposal_pending_final_amount_quote",
                schema: "events",
                table: "candidate_proposal",
                sql: "pending_final_amount_quote_attachment_id IS NULL OR pending_final_amount_amount IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_proposal_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal",
                column: "final_amount_quote_attachment_id",
                principalSchema: "events",
                principalTable: "proposal_attachment",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_proposal_pending_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal",
                column: "pending_final_amount_quote_attachment_id",
                principalSchema: "events",
                principalTable: "proposal_attachment",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_candidate_proposal_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_proposal_pending_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropIndex(
                name: "ix_candidate_proposal_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropIndex(
                name: "ix_candidate_proposal_pending_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_proposal_final_amount_quote",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_proposal_pending_final_amount_quote",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropColumn(
                name: "final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropColumn(
                name: "pending_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal");
        }
    }
}

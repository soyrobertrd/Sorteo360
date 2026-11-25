using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaffleLinkedEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "linked_event_id",
                schema: "events",
                table: "raffle",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_raffle_linked_event_id",
                schema: "events",
                table: "raffle",
                column: "linked_event_id");

            migrationBuilder.AddForeignKey(
                name: "fk_raffle_linked_event",
                schema: "events",
                table: "raffle",
                column: "linked_event_id",
                principalSchema: "events",
                principalTable: "event",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_raffle_linked_event",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropIndex(
                name: "ix_raffle_linked_event_id",
                schema: "events",
                table: "raffle");

            migrationBuilder.DropColumn(
                name: "linked_event_id",
                schema: "events",
                table: "raffle");
        }
    }
}

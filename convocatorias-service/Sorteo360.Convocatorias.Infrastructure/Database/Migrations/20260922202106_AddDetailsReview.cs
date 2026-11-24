using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <summary>
    /// El sello de revisión de una ficha que no coincide (spec <c>07</c> §6.1): cuándo y quién. Las
    /// dos anulables, porque una inscripción nace sin revisar.
    /// </summary>
    public partial class AddDetailsReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "details_reviewed_at",
                schema: "convocatorias",
                table: "registration",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "details_reviewed_by_id",
                schema: "convocatorias",
                table: "registration",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "details_reviewed_at",
                schema: "convocatorias",
                table: "registration");

            migrationBuilder.DropColumn(
                name: "details_reviewed_by_id",
                schema: "convocatorias",
                table: "registration");
        }
    }
}

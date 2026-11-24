using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ARoadshowCityKeepsItsTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Toda ciudad anterior queda en la hora de Santo Domingo, la que imprimió toda acta
            // emitida hasta hoy. El default sirve solo para ese relleno y se quita enseguida:
            // desde acá el valor por omisión lo pone el servicio, no la base.
            migrationBuilder.AddColumn<string>(
                name: "time_zone",
                schema: "events",
                table: "roadshow_city",
                type: "text",
                nullable: false,
                defaultValue: "America/Santo_Domingo");

            migrationBuilder.AlterColumn<string>(
                name: "time_zone",
                schema: "events",
                table: "roadshow_city",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "America/Santo_Domingo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "time_zone",
                schema: "events",
                table: "roadshow_city");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <summary>
    /// El id del mensaje que devuelve notification-service por destinatario (PGTI-177), para leer
    /// después si llegó. Nullable: las filas que ya existen no lo tienen y se siguen viendo como
    /// "enviado", que es lo que sabían.
    /// </summary>
    public partial class AddRecipientNotificationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "notification_id",
                schema: "convocatorias",
                table: "campaign_recipient",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "notification_id",
                schema: "convocatorias",
                table: "campaign_recipient");
        }
    }
}

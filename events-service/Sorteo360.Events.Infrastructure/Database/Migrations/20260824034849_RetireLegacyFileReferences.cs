using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RetireLegacyFileReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_supplier_photo_ref",
                schema: "events",
                table: "service_supplier_photo");

            // Las fotos que solo traían una referencia antigua no apuntan a ningún archivo: nunca
            // hubo bytes detrás de file_ref_id ni de url. No se puede rellenar lo que no existió,
            // así que la fila se va con la columna. Es una pérdida deliberada y acotada a este
            // momento: stage tiene datos de prueba y producción todavía no corre el stack.
            migrationBuilder.Sql(
                """
                DELETE FROM events.service_supplier_photo WHERE stored_file_id IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "file_ref_id",
                schema: "events",
                table: "service_supplier_photo");

            migrationBuilder.DropColumn(
                name: "url",
                schema: "events",
                table: "service_supplier_photo");

            migrationBuilder.DropColumn(
                name: "quote_photo_ref",
                schema: "events",
                table: "candidate");

            // El enlace pasa a ser obligatorio, y esa columna es lo que reemplaza al CHECK que
            // admitía tres referencias distintas. Sin defaultValue a propósito: un uuid de ceros
            // por defecto convertiría un enlace olvidado en una FK rota en vez de un rechazo.
            migrationBuilder.AlterColumn<Guid>(
                name: "stored_file_id",
                schema: "events",
                table: "service_supplier_photo",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Devuelve la forma del esquema, no los datos: las filas borradas arriba no vuelven.
            migrationBuilder.AlterColumn<Guid>(
                name: "stored_file_id",
                schema: "events",
                table: "service_supplier_photo",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "file_ref_id",
                schema: "events",
                table: "service_supplier_photo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "url",
                schema: "events",
                table: "service_supplier_photo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quote_photo_ref",
                schema: "events",
                table: "candidate",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_supplier_photo_ref",
                schema: "events",
                table: "service_supplier_photo",
                sql: "file_ref_id IS NOT NULL OR url IS NOT NULL OR stored_file_id IS NOT NULL");
        }
    }
}

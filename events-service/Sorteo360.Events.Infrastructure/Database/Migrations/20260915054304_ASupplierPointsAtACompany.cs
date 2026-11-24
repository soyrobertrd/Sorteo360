using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Un proveedor deja de ser una empresa y pasa a apuntar a una del CRM (ADR-0015): se van las
    /// columnas de identidad y llega <c>company_id</c> obligatoria, en un solo paso y sin script de
    /// backfill.
    /// <para>
    /// Sin backfill porque habría tenido que inventar empresas del CRM a partir de nombres en texto
    /// libre que nunca se deduplicaron, metiendo cada errata y cada par "Hotel Jaragua" / "Jaragua
    /// Hotel" en el maestro cuyo propósito es precisamente deduplicar. Los datos de proveedores de
    /// stage son desechables por decisión.
    /// </para>
    /// <para>
    /// <c>location_map_url</c> se borra y no se mueve: el cliente arma el enlace al mapa con el
    /// domicilio que resuelve del CRM, así que no puede haber una URL guardada que lo contradiga.
    /// </para>
    /// <para>
    /// Las filas que ya existieran reciben cada una un <c>gen_random_uuid()</c> distinto y después se
    /// quita el default. Un mismo valor en todas no serviría: dos proveedores de la misma categoría
    /// chocarían contra el índice único nuevo. Y borrarlas tampoco: <c>candidate</c> las referencia
    /// por clave foránea, así que borrar un proveedor se llevaría por delante cotizaciones ya
    /// decididas, que es mucho peor que una ficha cuya empresa no resuelve. Un entorno nuevo no pasa
    /// por acá con ninguna fila, así que esto solo toca los datos desechables de desarrollo y stage.
    /// </para>
    /// <para>
    /// Esas fichas quedan apuntando a una empresa que el CRM no conoce. Es una lectura que devuelve
    /// un id que no resuelve, no una que se caiga: la degradación que le pone el nombre en
    /// <c>null</c> llega con la tarjeta que resuelve identidad contra el directorio del CRM.
    /// </para>
    /// </summary>
    public partial class ASupplierPointsAtACompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_supplier_active",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "address",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "company",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "contact_email",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "contact_last_name",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "contact_name",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "country",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "location_map_url",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                schema: "events",
                table: "service_supplier",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            // El default era solo para poder llegar a NOT NULL con filas ya escritas. Se quita
            // enseguida: el modelo no declara ninguno, y dejarlo haría que EF omitiera la columna
            // del INSERT cuando el valor coincidiera con el centinela, escribiendo una empresa al
            // azar en lugar de la que pidió quien llama.
            migrationBuilder.Sql(
                "ALTER TABLE events.service_supplier ALTER COLUMN company_id DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_active",
                schema: "events",
                table: "service_supplier",
                column: "category",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_service_supplier_company_category",
                schema: "events",
                table: "service_supplier",
                columns: new[] { "company_id", "category" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_supplier_active",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropIndex(
                name: "ux_service_supplier_company_category",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.DropColumn(
                name: "company_id",
                schema: "events",
                table: "service_supplier");

            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "contact_last_name",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_name",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "location_map_url",
                schema: "events",
                table: "service_supplier",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_active",
                schema: "events",
                table: "service_supplier",
                columns: new[] { "category", "country" },
                filter: "deleted_at IS NULL");
        }
    }
}

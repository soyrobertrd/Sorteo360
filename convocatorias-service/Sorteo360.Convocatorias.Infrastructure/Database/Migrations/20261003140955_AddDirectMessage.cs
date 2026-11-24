using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <summary>
    /// La campaña sin evento (PGTI-174): <c>event_id</c> pasa a admitir nulos, la fila gana el asunto
    /// y el texto de un mensaje directo, y se siembra la plantilla <c>direct_message</c> en <c>es</c>.
    /// Las filas que ya existen quedan con las dos columnas nuevas nulas y cumplen la restricción
    /// nueva tal como están.
    /// <para>
    /// La plantilla se inserta con <c>ON CONFLICT DO NOTHING</c> porque <c>AddTemplates</c> lee
    /// <c>TemplateSeed</c> en vivo: una base nueva ya la recibe de ahí, y esta inserción es entonces
    /// un no-op. Una base existente la recibe de acá. Las dos convergen en la misma fila.
    /// </para>
    /// </summary>
    public partial class AddDirectMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "event_id",
                schema: "convocatorias",
                table: "campaign",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "message",
                schema: "convocatorias",
                table: "campaign",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject",
                schema: "convocatorias",
                table: "campaign",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_campaign_direct_message_is_whole",
                schema: "convocatorias",
                table: "campaign",
                sql: "(event_id IS NULL) = (template_key = 'direct_message') AND (event_id IS NULL) = (subject IS NOT NULL) AND (event_id IS NULL) = (message IS NOT NULL)");

            var template = DirectMessageTemplate();

            migrationBuilder.Sql(
                "INSERT INTO convocatorias.template"
                + " (template_key, language, subject, body_html, purpose, is_campaign_selectable)"
                + $" VALUES ({Literal(template.TemplateKey)}, {Literal(template.Language)}, {Literal(template.Subject)},"
                + $" {Literal(template.BodyHtml)}, {Literal(template.Purpose)}, {(template.IsCampaignSelectable ? "TRUE" : "FALSE")})"
                + " ON CONFLICT (template_key, language) DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un mensaje directo no tiene evento y no cabe en la columna obligatoria de antes: se
            // va con sus destinatarios y exclusiones (en cascada), y la plantilla con él.
            migrationBuilder.Sql("DELETE FROM convocatorias.campaign WHERE event_id IS NULL;");

            migrationBuilder.DeleteData(
                schema: "convocatorias",
                table: "template",
                keyColumns: ["template_key", "language"],
                keyValues: [TemplateKey.DirectMessage, AllowedLanguage.Espanol]);

            migrationBuilder.DropCheckConstraint(
                name: "ck_campaign_direct_message_is_whole",
                schema: "convocatorias",
                table: "campaign");

            migrationBuilder.DropColumn(
                name: "message",
                schema: "convocatorias",
                table: "campaign");

            migrationBuilder.DropColumn(
                name: "subject",
                schema: "convocatorias",
                table: "campaign");

            migrationBuilder.AlterColumn<Guid>(
                name: "event_id",
                schema: "convocatorias",
                table: "campaign",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <summary>
        /// La fila tal como la declara <c>TemplateSeed</c>, igual que hacen <c>AddTemplates</c> y las
        /// migraciones de copia: lo que aterriza es byte por byte lo que el dominio declara.
        /// </summary>
        private static Template DirectMessageTemplate() => TemplateSeed.Rows
            .Single(template =>
                template.TemplateKey == TemplateKey.DirectMessage
                && template.Language == AllowedLanguage.Espanol);

        /// <summary>Un literal de texto de SQL, con sus comillas simples duplicadas.</summary>
        private static string Literal(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}

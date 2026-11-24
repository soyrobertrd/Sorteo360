using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <summary>
    /// El aviso de cancelación (spec <c>06</c> §4.7): la columna <c>cancellation_notified_at</c>,
    /// que es lo que vuelve idempotente el aviso, y la referencia en el cuerpo de
    /// <c>event_cancelled</c> en <c>es</c>, para que una respuesta confundida se pueda rastrear.
    /// <para>
    /// La copia se actualiza como la de <c>UpdateRegistrationConfirmationBody</c>: una base
    /// existente se quedó con el cuerpo que sembró <c>AddTemplates</c>, y una nueva ya recibe el de
    /// hoy, así que este <c>UPDATE</c> es ahí un no-op. Las dos convergen en el mismo cuerpo.
    /// </para>
    /// </summary>
    public partial class AddCancellationNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancellation_notified_at",
                schema: "convocatorias",
                table: "registration",
                type: "timestamp with time zone",
                nullable: true);

            // El cuerpo nuevo se toma de TemplateSeed, la copia del dominio, igual que AddTemplates.
            migrationBuilder.UpdateData(
                schema: "convocatorias",
                table: "template",
                keyColumns: ["template_key", "language"],
                keyValues: [TemplateKey.EventCancelled, AllowedLanguage.Espanol],
                column: "body_html",
                value: EventCancelledBody());

            static string EventCancelledBody() => TemplateSeed.Rows
                .Single(template =>
                    template.TemplateKey == TemplateKey.EventCancelled
                    && template.Language == AllowedLanguage.Espanol)
                .BodyHtml;
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El cuerpo anterior, en literal: al revertir no se puede leer de TemplateSeed, que ya
            // trae el nuevo. No lleva la referencia.
            migrationBuilder.UpdateData(
                schema: "convocatorias",
                table: "template",
                keyColumns: ["template_key", "language"],
                keyValues: [TemplateKey.EventCancelled, AllowedLanguage.Espanol],
                column: "body_html",
                value: PreviousBody);

            migrationBuilder.DropColumn(
                name: "cancellation_notified_at",
                schema: "convocatorias",
                table: "registration");
        }

        private const string PreviousBody =
            """
            <html lang="es">
              <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
                  <tr>
                    <td><img src="https://godominicanrepublic.com/assets/convocatorias/roadshow.jpg" alt="Ministerio de Turismo de la República Dominicana" width="600" style="display:block;width:100%;height:auto;"></td>
                  </tr>
                  <tr>
                    <td style="padding:24px;">
                      <p>Estimado/a {{nombre}}:</p>
                      <p>Lamentamos informarle que <strong>{{evento}}</strong>, previsto para {{fecha}}, ha sido cancelado.</p>
                      <p>Su registro queda sin efecto y no tiene que hacer ninguna gestión.</p>
                      <p>Agradecemos su interés y lamentamos las molestias.</p>
                      <p>Cordialmente,<br>Ministerio de Turismo de la República Dominicana</p>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }
}

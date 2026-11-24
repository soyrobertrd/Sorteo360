using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Añade la referencia como texto y el enlace a la entrada al cuerpo de
    /// <c>registration_confirmation</c> en <c>es</c> (spec <c>06</c> §4.4). Un arreglo de copia se
    /// despacha como una migración de datos: <c>AddTemplates</c> insertó lo que decía
    /// <c>TemplateSeed</c> el día que corrió, así que una base existente se quedó con el cuerpo
    /// viejo hasta que esto lo actualiza. Una base nueva ya recibe el cuerpo nuevo de
    /// <c>AddTemplates</c> —que lee <c>TemplateSeed</c> en vivo— y este <c>UPDATE</c> es entonces
    /// un no-op que la deja igual. Las dos convergen en el mismo cuerpo, que es lo que comprueba
    /// <c>Tests.Integration/Database/TemplateSeedTests</c>.
    /// </summary>
    public partial class UpdateRegistrationConfirmationBody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El cuerpo nuevo se toma de TemplateSeed, la copia del dominio, igual que AddTemplates:
            // así el valor que aterriza en la base es byte por byte el que el dominio declara, y no
            // una segunda copia que puede desviarse de él.
            migrationBuilder.UpdateData(
                schema: "convocatorias",
                table: "template",
                keyColumns: ["template_key", "language"],
                keyValues: [TemplateKey.RegistrationConfirmation, AllowedLanguage.Espanol],
                column: "body_html",
                value: RegistrationConfirmationBody());

            static string RegistrationConfirmationBody() => TemplateSeed.Rows
                .Single(template =>
                    template.TemplateKey == TemplateKey.RegistrationConfirmation
                    && template.Language == AllowedLanguage.Espanol)
                .BodyHtml;
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El cuerpo anterior, en literal: al revertir no se puede leer de TemplateSeed, que ya
            // trae el nuevo. No lleva la referencia ni el enlace a la entrada.
            migrationBuilder.UpdateData(
                schema: "convocatorias",
                table: "template",
                keyColumns: ["template_key", "language"],
                keyValues: [TemplateKey.RegistrationConfirmation, AllowedLanguage.Espanol],
                column: "body_html",
                value: PreviousBody);
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
                      <p>Su registro a <strong>{{evento}}</strong> quedó confirmado.</p>
                      <p><strong>Lugar:</strong> {{lugar}}<br><strong>Fecha:</strong> {{fecha}}</p>
                      <p style="text-align:center;padding:16px 0;">
                        <img src="{{qr}}" alt="Código QR de su registro" width="200" style="display:block;margin:0 auto;">
                      </p>
                      <p>Presente este código en la entrada el día del evento.</p>
                      <p>Cordialmente,<br>Ministerio de Turismo de la República Dominicana</p>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }
}

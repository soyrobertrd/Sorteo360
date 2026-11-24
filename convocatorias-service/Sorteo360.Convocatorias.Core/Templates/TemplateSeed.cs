using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Core.Templates;

/// <summary>
/// El conjunto sembrado, cinco claves en <c>es</c>. Es la copia que siembra la migración de
/// datos <c>AddTemplates</c>, y es lo que recorren los tests de marcadores derivados.
/// <para>
/// <b>Esta clase está congelada.</b> Es el contenido de una migración ya aplicada, así que
/// editarla cambiaría lo que recibe una base nueva sin cambiar lo que tiene una base existente,
/// que es exactamente la clase de divergencia silenciosa por la que no se usa <c>HasData</c>.
/// Un arreglo de copia se despacha como una migración nueva con sus propios valores.
/// </para>
/// <para>
/// <b>La copia es provisional.</b> MITUR ya manda estos correos en el sistema que esta
/// plataforma reemplaza y su dueño está recuperando los textos reales, en <c>es</c>, <c>en</c> y
/// <c>fr</c>. El modelo está decidido; la lista de claves, el conjunto soportado y las dos
/// reglas de grano son provisionales contra lo que traigan esas plantillas.
/// </para>
/// </summary>
public static class TemplateSeed
{
    /// <summary>
    /// El arte, dentro del cuerpo y como <c>&lt;img src&gt;</c> <c>https://</c>. Es el mismo para
    /// todo roadshow hoy, así que se cambia como se cambia la copia: no hay columna
    /// <c>arte_url</c>, ni endpoint de subida, ni comprobación de que la URL responda. Revisar
    /// mil URLs en un envío convierte un CDN inestable en una campaña fallida, y un cliente de
    /// correo que no puede cargar la imagen ya muestra el texto alternativo.
    /// </summary>
    private const string ArtworkUrl = "https://godominicanrepublic.com/assets/convocatorias/roadshow.jpg";

    /// <summary>Las filas sembradas, en el orden en que las inserta la migración.</summary>
    public static IReadOnlyList<Template> Rows { get; } =
    [
        new Template(
            TemplateKey.SaveTheDate,
            AllowedLanguage.Espanol,
            "Reserve la fecha: {{evento}}",
            SaveTheDateBody,
            TemplatePurpose.Campaign,
            isCampaignSelectable: true),

        new Template(
            TemplateKey.RegistrationInvite,
            AllowedLanguage.Espanol,
            "Invitación a {{evento}}",
            RegistrationInviteBody,
            TemplatePurpose.Campaign,
            isCampaignSelectable: true),

        new Template(
            TemplateKey.RegistrationConfirmation,
            AllowedLanguage.Espanol,
            "Su registro a {{evento}} está confirmado",
            RegistrationConfirmationBody,
            TemplatePurpose.Transactional,
            isCampaignSelectable: false),

        new Template(
            TemplateKey.EventCancelled,
            AllowedLanguage.Espanol,
            "{{evento}} ha sido cancelado",
            EventCancelledBody,
            TemplatePurpose.Transactional,
            isCampaignSelectable: false),

        // No seleccionable: no aparece en el selector de una campaña de evento, porque una campaña
        // lleva esta plantilla si y solo si no tiene evento. Llegó después de las otras cuatro, con
        // su propia migración de datos (AddDirectMessage).
        new Template(
            TemplateKey.DirectMessage,
            AllowedLanguage.Espanol,
            "{{asunto}}",
            DirectMessageBody,
            TemplatePurpose.Campaign,
            isCampaignSelectable: false),
    ];

    private const string SaveTheDateBody =
        $$$"""
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td><img src="{{{ArtworkUrl}}}" alt="Sorteo360" width="600" style="display:block;width:100%;height:auto;"></td>
              </tr>
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>El equipo organizador de Sorteo360 le invita a reservar la fecha de <strong>{{evento}}</strong>.</p>
                  <p><strong>Lugar:</strong> {{lugar}}<br><strong>Fecha:</strong> {{fecha}}</p>
                  <p>En los próximos días recibirá la invitación con los detalles del programa y el enlace para registrarse.</p>
                  <p>Cordialmente,<br>Sorteo360</p>
                </td>
              </tr>
              <tr>
                <td style="padding:16px 24px;background:#f4f4f4;font-size:12px;color:#555555;">
                  <p>Recibe este correo porque figura en el directorio de contactos de Sorteo360.
                     <a href="{{enlace_baja}}">Darse de baja</a>.</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

    private const string RegistrationInviteBody =
        $$$"""
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td><img src="{{{ArtworkUrl}}}" alt="Sorteo360" width="600" style="display:block;width:100%;height:auto;"></td>
              </tr>
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>El equipo organizador de Sorteo360 tiene el gusto de invitarle a <strong>{{evento}}</strong>.</p>
                  <p><strong>Lugar:</strong> {{lugar}}<br><strong>Fecha:</strong> {{fecha}}</p>
                  <p style="text-align:center;padding:16px 0;">
                    <a href="{{enlace_registro}}" style="display:inline-block;padding:12px 28px;background:#00a0df;color:#ffffff;text-decoration:none;font-weight:bold;">Confirmar asistencia</a>
                  </p>
                  <p>El registro es personal. Si no puede asistir, puede reenviar esta invitación a la persona que le represente.</p>
                  <p>Cordialmente,<br>Sorteo360</p>
                </td>
              </tr>
              <tr>
                <td style="padding:16px 24px;background:#f4f4f4;font-size:12px;color:#555555;">
                  <p>Recibe este correo porque figura en el directorio de contactos de Sorteo360.
                     <a href="{{enlace_baja}}">Darse de baja</a>.</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

    private const string RegistrationConfirmationBody =
        $$$"""
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td><img src="{{{ArtworkUrl}}}" alt="Sorteo360" width="600" style="display:block;width:100%;height:auto;"></td>
              </tr>
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>Su registro a <strong>{{evento}}</strong> quedó confirmado.</p>
                  <p><strong>Lugar:</strong> {{lugar}}<br><strong>Fecha:</strong> {{fecha}}</p>
                  <p style="text-align:center;padding:16px 0;">
                    <img src="{{qr}}" alt="Código QR de su registro" width="200" style="display:block;margin:0 auto;">
                  </p>
                  <p style="text-align:center;"><strong>Referencia:</strong> {{referencia}}</p>
                  <p>Presente este código en la entrada el día del evento. También puede ver su entrada en línea: <a href="{{enlace_entrada}}">ver mi entrada</a>.</p>
                  <p>Cordialmente,<br>Sorteo360</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

    /// <summary>
    /// El mensaje directo: el texto de quien armó la campaña, sin enlace de registro porque no hay
    /// evento al que inscribirse, y con el de baja porque sigue siendo una campaña. El texto es
    /// plano y se escapa como cualquier valor; <c>white-space:pre-line</c> es lo que conserva sus
    /// saltos de línea.
    /// </summary>
    private const string DirectMessageBody =
        $$$"""
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td><img src="{{{ArtworkUrl}}}" alt="Sorteo360" width="600" style="display:block;width:100%;height:auto;"></td>
              </tr>
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <div style="white-space:pre-line;">{{mensaje}}</div>
                  <p>Cordialmente,<br>Sorteo360</p>
                </td>
              </tr>
              <tr>
                <td style="padding:16px 24px;background:#f4f4f4;font-size:12px;color:#555555;">
                  <p>Recibe este correo porque figura en el directorio de contactos de Sorteo360.
                     <a href="{{enlace_baja}}">Darse de baja</a>.</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

    private const string EventCancelledBody =
        $$$"""
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td><img src="{{{ArtworkUrl}}}" alt="Sorteo360" width="600" style="display:block;width:100%;height:auto;"></td>
              </tr>
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>Lamentamos informarle que <strong>{{evento}}</strong>, previsto para {{fecha}}, ha sido cancelado.</p>
                  <p>Su registro queda sin efecto y no tiene que hacer ninguna gestión.</p>
                  <p><strong>Referencia de su registro:</strong> {{referencia}}</p>
                  <p>Agradecemos su interés y lamentamos las molestias.</p>
                  <p>Cordialmente,<br>Sorteo360</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
}

using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.Templates;

/// <summary>
/// La copia sembrada por la migración de datos <c>AddQuoteOutcomeNotificationTemplates</c>: los dos
/// avisos que cierran un intento.
///
/// <para>
/// <b>Es una clase aparte y no dos filas más en <see cref="QuoteTemplateSeed"/>.</b> Aquella es el
/// contenido de una migración ya aplicada y está congelada: agregarle filas cambiaría lo que
/// inserta una base nueva sin tocar lo que ya tiene una base existente, que es exactamente la
/// divergencia silenciosa por la que no se usa <c>HasData</c>. Cada migración de datos se queda con
/// su propia copia, y esta queda congelada en cuanto se despliegue.
/// </para>
///
/// <para>
/// Las reglas son las mismas que ya fijó la del sometimiento y no se vuelven a discutir acá: idioma
/// fijo en <c>es</c> sin leer nunca <c>Event.Language</c>, sin montos, sin pie de baja y sin host
/// escrito a mano. El porqué de cada una está en <see cref="QuoteTemplateSeed"/>.
/// </para>
///
/// <para>
/// <b>Lo propio de estas dos es qué información viaja en el cuerpo.</b> La razón del rechazo va
/// entera y no detrás del enlace: obligar a alguien a entrar para leer una frase es justo la
/// costumbre que entrena a la gente a ignorar el correo. El enlace sigue estando —es cómo se
/// <i>actúa</i>, no cómo se <i>entera</i>—. Y la aprobación nombra al proveedor ganador, que es el
/// único dato del desenlace que quien sometió no puede deducir.
/// </para>
///
/// <para>
/// La copia es provisional: se muestra al MITUR para su revisión, y lo que esta tarjeta fija es la
/// clave, las claves de datos y la forma del asunto, no la prosa.
/// </para>
/// </summary>
public static class QuoteOutcomeTemplateSeed
{
    /// <summary>Las filas sembradas, en el orden en que las inserta la migración.</summary>
    public static IReadOnlyList<NotificationTemplate> Rows { get; } =
    [
        new NotificationTemplate(
            "events.quote.approved",
            AllowedLanguage.Espanol,
            "Cotización aprobada: {{evento}}",
            ApprovedBody),

        new NotificationTemplate(
            "events.quote.rejected",
            AllowedLanguage.Espanol,
            "Cotización rechazada: {{evento}}",
            RejectedBody),
    ];

    /// <summary>
    /// El aviso de aprobación. Nombra al proveedor ganador porque es lo único que quien sometió no
    /// puede dar por hecho: recomendó una oferta, y Dirección pudo aprobar otra.
    /// </summary>
    private const string ApprovedBody =
        """
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>Se aprobó la cotización de <strong>{{categoria}}</strong> del evento
                     <strong>{{evento}}</strong>.</p>
                  <p>El proveedor seleccionado es <strong>{{proveedor}}</strong>.</p>
                  <p><a href="{{enlace}}">Ver la cotización</a></p>
                  <p>Cordialmente,<br>Equipo Organizador</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

    /// <summary>
    /// El aviso de rechazo. La razón es obligatoria al decidir y acá es el contenido: es la lista
    /// de pendientes de quien tiene que volver a someter.
    /// </summary>
    private const string RejectedBody =
        """
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>No se aprobó la cotización de <strong>{{categoria}}</strong> del evento
                     <strong>{{evento}}</strong>.</p>
                  <p>Motivo indicado por quien la revisó:</p>
                  <blockquote style="margin:0 0 16px;padding:12px 16px;border-left:4px solid #d0d0d0;background:#fafafa;">{{razon}}</blockquote>
                  <p>La cotización volvió a borrador y admite cambios para someterse de nuevo.</p>
                  <p><a href="{{enlace}}">Corregir la cotización</a></p>
                  <p>Cordialmente,<br>Equipo Organizador</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
}

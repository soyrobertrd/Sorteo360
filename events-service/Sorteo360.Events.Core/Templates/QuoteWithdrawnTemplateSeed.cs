using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.Templates;

/// <summary>
/// La copia sembrada por la migración de datos <c>AddQuoteWithdrawnNotificationTemplate</c>: el
/// cuarto y último aviso, el que dice que un pendiente desapareció de la fila de quien aprueba.
///
/// <para>
/// <b>Es una clase aparte, igual que <see cref="QuoteOutcomeTemplateSeed"/> lo es de
/// <see cref="QuoteTemplateSeed"/>, y por el mismo motivo.</b> Cada migración de datos se queda
/// con su propia copia congelada: agregarle una fila a la siembra de una migración ya aplicada
/// cambiaría lo que recibe una base nueva sin tocar lo que ya tiene una existente, que es
/// exactamente la divergencia silenciosa por la que no se usa <c>HasData</c>. Esta queda congelada
/// en cuanto se despliegue.
/// </para>
///
/// <para>
/// Las reglas comunes no se vuelven a discutir acá —idioma fijo en <c>es</c> sin leer nunca
/// <c>Event.Language</c>, sin montos, sin pie de baja y sin host escrito a mano—; el porqué de cada
/// una está en <see cref="QuoteTemplateSeed"/>.
/// </para>
///
/// <para>
/// <b>Lo propio de esta es a quién le habla y qué deja fuera.</b> Le habla a quienes aprueban y no
/// a quien sometió, incluso cuando quien retiró fue un colega —el porqué está en
/// <c>IQuoteNotifier.NotifyWithdrawnAsync</c>—, así que el tono es el de un pendiente que se
/// cierra y no el de un desenlace que se recibe. Y <b>no lleva la razón</b>: al retirar es
/// opcional (ADR-0011) y el dialecto de plantilla no tiene condicionales (spec <c>02</c> §5), de
/// modo que un <c>{{razon}}</c> acá llegaría como un bloque de cita vacío cada vez que quien
/// retiró no escribió nada. El rechazo sí la lleva porque ahí es obligatoria.
/// </para>
///
/// <para>
/// La copia es provisional: se muestra al MITUR para su revisión, y lo que esta tarjeta fija es la
/// clave, las claves de datos y la forma del asunto, no la prosa.
/// </para>
/// </summary>
public static class QuoteWithdrawnTemplateSeed
{
    /// <summary>La fila sembrada.</summary>
    public static IReadOnlyList<NotificationTemplate> Rows { get; } =
    [
        new NotificationTemplate(
            "events.quote.withdrawn",
            AllowedLanguage.Espanol,
            "Cotización retirada: {{evento}}",
            WithdrawnBody),
    ];

    /// <summary>
    /// El aviso de retiro. Dice que ya no hay nada que decidir y que la oficina puede volver a
    /// someter, que es todo lo que su destinataria necesita para dejar de esperarlo.
    /// </summary>
    private const string WithdrawnBody =
        """
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>La oficina retiró la cotización de <strong>{{categoria}}</strong> del evento
                     <strong>{{evento}}</strong>, que estaba pendiente de su decisión.</p>
                  <p>Ya no requiere ninguna acción de su parte. La oficina puede volver a someterla
                     con cambios.</p>
                  <p><a href="{{enlace}}">Ver el evento</a></p>
                  <p>Cordialmente,<br>Equipo Organizador</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
}

using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.Templates;

/// <summary>
/// La copia sembrada por la migración de datos <c>AddQuoteNotificationTemplates</c>.
///
/// <para>
/// <b>Esta clase está congelada.</b> Es el contenido de una migración ya aplicada: editarla
/// cambiaría lo que recibe una base nueva sin cambiar lo que tiene una base existente, que es
/// exactamente la divergencia silenciosa por la que no se usa <c>HasData</c>. Un arreglo de copia
/// se despacha como una migración nueva con sus propios valores.
/// </para>
///
/// <para>
/// <b>El idioma está fijo en <c>es</c> y <c>Event.Language</c> no se lee nunca.</b> Es la fuente
/// que parece obvia y está mal dos veces: es anulable, y describe el <i>evento</i> —una feria en
/// Madrid, un roadshow en Nueva York— y no a la persona que lee el aviso. Leerla le escribiría en
/// inglés a una coordinadora dominicana porque su feria es en Nueva York.
/// </para>
///
/// <para>
/// <b>Sin montos, en ningún lado.</b> La prueba que se aplica es qué no podemos des-enviar: una
/// bandeja de entrada queda fuera de la autorización de la aplicación y un reenvío no se puede
/// recuperar, y el dinero es la única parte de este flujo que contesta que sí. Agregarlo más
/// adelante cuesta una fila de plantilla.
/// </para>
///
/// <para>
/// <b>Sin pie de baja.</b> Todo destinatario interno es contactable sin condiciones, y no se le
/// puede permitir a nadie darse de baja de su propio trabajo. Se escribe para que a nadie le dé
/// por agregarlo después buscando consistencia con las plantillas de campaña de convocatorias.
/// </para>
///
/// <para>
/// <b>Sin host escrito a mano.</b> El único enlace es <c>{{enlace}}</c>, que arma
/// <see cref="EventLink"/> desde <c>App__BaseUrl</c>.
/// </para>
///
/// <para>
/// La copia es provisional: se muestra al MITUR para su revisión, y lo que esta tarjeta fija es la
/// clave, las claves de datos y la forma del asunto, no la prosa.
/// </para>
/// </summary>
public static class QuoteTemplateSeed
{
    /// <summary>Las filas sembradas, en el orden en que las inserta la migración.</summary>
    public static IReadOnlyList<NotificationTemplate> Rows { get; } =
    [
        new NotificationTemplate(
            "events.quote.submitted",
            AllowedLanguage.Espanol,
            "Cotización pendiente de aprobación: {{evento}}",
            SubmittedBody),
    ];

    /// <summary>
    /// El aviso de sometimiento. Dice qué se sometió y dónde decidirlo, y nada más: quien lo lee
    /// tiene que abrir el evento igual para ver las ofertas, así que repetirlas acá solo agregaría
    /// una copia de datos que puede quedar vieja dentro de una bandeja de entrada.
    /// </summary>
    private const string SubmittedBody =
        """
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>La oficina sometió a aprobación la cotización de <strong>{{categoria}}</strong>
                     del evento <strong>{{evento}}</strong>.</p>
                  <p>Queda pendiente de su decisión.</p>
                  <p><a href="{{enlace}}">Revisar la cotización</a></p>
                  <p>Cordialmente,<br>Equipo Organizador</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
}

using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.Templates;

/// <summary>
/// La copia sembrada por la migración de datos <c>AddPrizeWonNotificationTemplates</c>: el correo al
/// ganador de cada sorteo de premio vivo cuando el sorteo se completa (spec 03 §5).
///
/// <para>
/// <b>Es una clase aparte y queda congelada en cuanto se despliegue</b>, por la misma razón que
/// <see cref="QuoteWithdrawnTemplateSeed"/>: editarla cambiaría lo que recibe una base nueva sin
/// tocar lo que ya tiene una existente. Cambiar la copia es una migración nueva.
/// </para>
///
/// <para>
/// <b>Lo propio de esta es que tiene dos idiomas.</b> Quien la lee no es personal del MITUR sino un
/// asistente, muchas veces extranjero, así que el notificador la busca en <c>Event.Language</c> y cae
/// a <c>es</c> si no hay copia en ese idioma (spec 03 §5.4). Las claves de datos son las mismas en
/// las dos, en español, para que una sola bolsa sirva a cualquier idioma.
/// </para>
///
/// <para>
/// <b>Lo que no lleva:</b> enlace —el ganador no tiene acceso a la aplicación—, patrocinador —es
/// optativo y el dialecto no tiene condicionales, así que quedaría una frase colgando—, valor del
/// premio, tamaño del bombo ni el nombre de nadie más (spec 03 §5.3). Las instrucciones van con
/// <c>white-space:pre-line</c> para conservar los saltos de línea del operador, mientras
/// notification-service sigue escapando el valor.
/// </para>
///
/// <para>
/// La copia es provisional: la revisa el MITUR en el PR.
/// </para>
/// </summary>
public static class PrizeWonTemplateSeed
{
    /// <summary>Las filas sembradas: español e inglés.</summary>
    public static IReadOnlyList<NotificationTemplate> Rows { get; } =
    [
        new NotificationTemplate(
            RaffleNotificationType.PrizeWon,
            AllowedLanguage.Espanol,
            "¡Felicidades! Usted ganó {{premio}} en {{evento}}",
            SpanishBody),
        new NotificationTemplate(
            RaffleNotificationType.PrizeWon,
            AllowedLanguage.Ingles,
            "Congratulations! You won {{premio}} at {{evento}}",
            EnglishBody),
    ];

    private const string SpanishBody =
        """
        <html lang="es">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td style="padding:24px;">
                  <p>Estimado/a {{nombre}}:</p>
                  <p>¡Felicidades! Usted ganó <strong>{{premio}}</strong> en el sorteo de
                     <strong>{{evento}}</strong> ({{fechas}}).</p>
                  <p>Su código de retiro es:</p>
                  <p style="margin:16px 0;padding:16px;border:2px dashed #1a1a1a;text-align:center;font-family:'Courier New',Courier,monospace;font-size:28px;font-weight:bold;letter-spacing:2px;">{{codigo}}</p>
                  <p>Para retirar su premio, presente este código junto con su documento de identidad.</p>
                  <p><strong>Dónde y hasta cuándo retirarlo:</strong></p>
                  <p style="white-space:pre-line;">{{instrucciones}}</p>
                  <p>Cordialmente,<br>{{organizador}}</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

    private const string EnglishBody =
        """
        <html lang="en">
          <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;">
              <tr>
                <td style="padding:24px;">
                  <p>Dear {{nombre}},</p>
                  <p>Congratulations! You won <strong>{{premio}}</strong> in the raffle at
                     <strong>{{evento}}</strong> ({{fechas}}).</p>
                  <p>Your pickup code is:</p>
                  <p style="margin:16px 0;padding:16px;border:2px dashed #1a1a1a;text-align:center;font-family:'Courier New',Courier,monospace;font-size:28px;font-weight:bold;letter-spacing:2px;">{{codigo}}</p>
                  <p>To collect your prize, please present this code together with your identity document.</p>
                  <p><strong>Where and until when to collect it:</strong></p>
                  <p style="white-space:pre-line;">{{instrucciones}}</p>
                  <p>Kind regards,<br>{{organizador}}</p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
}

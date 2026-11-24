using Sorteo360.Notifications.Core.Notifications;

namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// Cómo un evento del webhook mueve —o no— el estado de una fila de persona.
/// <para>
/// <b>El registro de eventos es de solo añadir y el estado se deriva, no se sobrescribe.</b> El
/// webhook es at-least-once y desordenado: los lotes se agrupan y cada evento reintenta por su
/// cuenta durante 24 horas, así que un <c>processed</c> tardío detrás de un <c>delivered</c> es el
/// caso corriente y no el raro. Derivar significa quedarse con el evento que más lejos llega en la
/// escalera, y por eso se toma un máximo en vez de asignar.
/// </para>
/// <para>
/// Tomar el máximo de a un evento equivale a recalcular sobre el registro entero, porque el máximo
/// es asociativo y conmutativo. Eso es lo que permite escribir el estado al insertar cada evento
/// sin releer toda la historia de la fila.
/// </para>
/// <para>
/// <b>Normativo: la entrega y la reacción de quien recibe son dos hechos distintos y no comparten
/// columna.</b> Un reporte de spam significa que el correo <i>sí</i> se entregó. Un borrador
/// anterior puso <c>spamreport</c> encima de <c>delivered</c> en una sola escalera de precedencia;
/// eso es un error de modelado, y queda escrito acá para no volver a cometerlo. Son dos escaleras
/// —<see cref="Delivery"/> y <see cref="Action"/>— con la misma mecánica y ningún peldaño en común.
/// </para>
/// </summary>
public static class DeliveryLadder
{
    /// <summary>
    /// Qué tan lejos llega cada estado de entrega. Lo de la especificación —
    /// <c>bounced</c>/<c>dropped</c> &gt; <c>delivered</c> &gt; <c>deferred</c> &gt; <c>sent</c>
    /// &gt; <c>queued</c>— más <c>failed</c>, que la especificación no ordena porque no lo escribe
    /// el webhook: es nuestra transacción SMTP muriendo, el mensaje nunca llegó al proveedor, y va
    /// arriba del todo para que un evento perdido no pueda resucitar una fila que sabemos muerta.
    /// </summary>
    private static readonly Ladder Delivery = new(
        [DeliveryStatus.Queued],
        [DeliveryStatus.Sent],
        [DeliveryStatus.Deferred],
        [DeliveryStatus.Delivered],
        [DeliveryStatus.Bounced, DeliveryStatus.Dropped, DeliveryStatus.Failed]);

    /// <summary>
    /// Qué tan lejos llega cada reacción. Una queja pesa más que una baja: quien reporta spam
    /// además se dio de baja de hecho, y la vuelta no es cierta.
    /// </summary>
    private static readonly Ladder Action = new(
        [RecipientAction.None],
        [RecipientAction.Unsubscribed],
        [RecipientAction.Complained]);

    /// <summary>
    /// El estado de entrega que afirma un tipo de evento, o <c>null</c> si no afirma ninguno.
    /// <para>
    /// <c>spamreport</c> afirma <c>delivered</c>, y no es una licencia: un correo no se puede
    /// reportar como spam sin haber llegado. Como el avance toma el máximo, esto nunca degrada un
    /// <c>bounced</c> y sí convierte en cierto lo que la especificación afirma.
    /// </para>
    /// <para>
    /// <c>unsubscribe</c> no afirma ninguno: una baja habla de la persona, no del mensaje. Un tipo
    /// desconocido tampoco, que es lo que lo deja guardado e inerte.
    /// </para>
    /// </summary>
    public static string? DeliveryStatusFor(string eventType) => eventType switch
    {
        SendGridEventTypes.Processed => DeliveryStatus.Sent,
        SendGridEventTypes.Deferred => DeliveryStatus.Deferred,
        SendGridEventTypes.Delivered => DeliveryStatus.Delivered,
        SendGridEventTypes.Bounce => DeliveryStatus.Bounced,
        SendGridEventTypes.Dropped => DeliveryStatus.Dropped,
        SendGridEventTypes.SpamReport => DeliveryStatus.Delivered,
        _ => null
    };

    /// <summary>La reacción que afirma un tipo de evento, o <c>null</c> si no afirma ninguna.</summary>
    public static string? RecipientActionFor(string eventType) => eventType switch
    {
        SendGridEventTypes.Unsubscribe => RecipientAction.Unsubscribed,
        SendGridEventTypes.SpamReport => RecipientAction.Complained,
        _ => null
    };

    /// <summary>
    /// El motivo con el que se retira la dirección de circulación, o <c>null</c> si el evento no la
    /// retira.
    /// <para>
    /// Son los dos que la persona pide explícitamente. Un rebote no entra: la supresión por buzón
    /// la maneja el proveedor, y mezclarla con el consentimiento haría que una dirección mal
    /// escrita se viera como una baja, que no se recupera desde la pantalla de consentimiento.
    /// </para>
    /// <para>
    /// El motivo vive acá, junto a las otras dos preguntas sobre el tipo de evento, y no en la capa
    /// de persistencia: es conocimiento del vocabulario del proveedor, no de una tabla.
    /// </para>
    /// </summary>
    public static string? SuppressionReasonFor(string eventType) => eventType switch
    {
        SendGridEventTypes.Unsubscribe => SendGridEventTypes.Unsubscribe,
        SendGridEventTypes.SpamReport => SendGridEventTypes.SpamReport,
        _ => null
    };

    /// <summary>
    /// El estado que queda tras aplicar <paramref name="incoming"/> sobre
    /// <paramref name="current"/>: el que más lejos llega de los dos.
    /// </summary>
    public static string Advance(string current, string? incoming) => Delivery.Max(current, incoming);

    /// <summary>La reacción que queda tras aplicar la entrante sobre la actual, con la misma regla.</summary>
    public static string AdvanceAction(string current, string? incoming) => Action.Max(current, incoming);

    /// <summary>
    /// Una escalera de valores ordenada por peldaños, y la única operación que se le hace: quedarse
    /// con el que llega más lejos. Existe para que las dos escaleras compartan mecánica sin
    /// compartir peldaños.
    /// </summary>
    private sealed class Ladder
    {
        private readonly Dictionary<string, int> _rank = new(StringComparer.Ordinal);

        /// <param name="rungs">
        /// Los peldaños, de abajo hacia arriba. Un peldaño puede llevar más de un valor cuando son
        /// finales distintos que valen lo mismo, como <c>bounced</c> y <c>dropped</c>.
        /// </param>
        public Ladder(params string[][] rungs)
        {
            for (var rung = 0; rung < rungs.Length; rung++)
            {
                foreach (var value in rungs[rung])
                    _rank[value] = rung;
            }
        }

        /// <summary>
        /// El que más lejos llega de los dos. Un <paramref name="incoming"/> nulo —el evento no
        /// afirma nada— deja el actual intacto, y un valor que no está en la escalera cuenta como
        /// más bajo que cualquiera, así que nunca gana por desconocido.
        /// </summary>
        public string Max(string current, string? incoming)
        {
            ArgumentNullException.ThrowIfNull(current);

            if (incoming is null)
                return current;

            return RankOf(incoming) > RankOf(current) ? incoming : current;
        }

        private int RankOf(string value) => _rank.TryGetValue(value, out var rank) ? rank : -1;
    }
}

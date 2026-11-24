using Sorteo360.Notifications.Core.Notifications;

namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// A qué fila de persona pertenece un evento: una escalera de tres peldaños que termina siempre en
/// algo escrito.
/// <para>
/// Hacen falta tres peldaños porque el proveedor documenta dos agujeros, y los dos caen justo sobre
/// el evento que más le importa al cliente: los <c>unique_args</c> <b>no se adjuntan a los rebotes
/// con Return-Path</b>, y el <c>sg_message_id</c> <b>falta en los rebotes diferidos o
/// asíncronos</b>. Un evento que no nombra a nadie es normal, no una anomalía.
/// </para>
/// <para>
/// <b>Normativo: nunca adivinar, nunca descartar.</b> Un huérfano es una fila de
/// <c>notification_event</c> con <c>recipient_id</c> en null: registrada, visible y enlazable
/// después rellenando la columna. Eso también resuelve gratis que stage y producción compartan una
/// cuenta del proveedor: un evento de un GUID que este entorno nunca emitió se vuelve huérfano en
/// vez de corromper una fila ajena.
/// </para>
/// </summary>
public static class EventCorrelation
{
    /// <summary>
    /// Los estados en los que un envío sigue <b>abierto</b>: se lo entregamos al proveedor y él
    /// todavía no dijo en qué terminó.
    /// <para>
    /// Son los únicos candidatos del peldaño 2. <c>queued</c> queda fuera porque nunca salió de
    /// acá, y los terminales quedan fuera porque su historia ya está contada; incluir un
    /// <c>delivered</c> haría que un rebote tardío pudiera aterrizar sobre un envío anterior y
    /// exitoso a la misma dirección.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> OpenStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        DeliveryStatus.Sent,
        DeliveryStatus.Deferred
    };

    /// <summary>
    /// La fila a la que enlazar, o <c>null</c> para guardarlo como huérfano.
    /// </summary>
    /// <param name="correlationId">
    /// El GUID opaco que viajó en <c>unique_args</c>, si vino alguno.
    /// </param>
    /// <param name="correlationIdExists">
    /// Si ese GUID corresponde a una fila de este entorno. Un GUID que vino pero no existe es
    /// huérfano y <b>no</b> baja al peldaño 2: el evento sí nombra a alguien, solo que a nadie de
    /// acá, y buscar por dirección entonces sería inventar un enlace que el propio evento
    /// contradice.
    /// </param>
    /// <param name="openSendsToTheAddress">
    /// Las filas abiertas hacia la dirección del evento. Solo se enlaza con <b>exactamente una</b>:
    /// con dos no hay forma de saber cuál, y elegir sería adivinar.
    /// <para>
    /// Se vuelven a consultar por cada evento, también dentro de un mismo lote, y eso tiene una
    /// consecuencia que se eligió a conciencia: si un primer evento sin GUID cierra la única fila
    /// abierta de una dirección, un segundo evento sin GUID hacia esa misma dirección ya no la
    /// encuentra abierta y se guarda como huérfano. Es lo que dice el peldaño 2 al pie de la letra
    /// —<i>"solo cuando exactamente un envío a esa dirección sigue abierto"</i>—; recordar a quién
    /// se acaba de enlazar sería inventar un cuarto peldaño que la especificación no tiene, y "nunca
    /// adivinar" pesa más que la comodidad. El huérfano queda visible y se puede enlazar después.
    /// </para>
    /// </param>
    public static Guid? Resolve(
        Guid? correlationId,
        bool correlationIdExists,
        IReadOnlyCollection<Guid> openSendsToTheAddress)
    {
        ArgumentNullException.ThrowIfNull(openSendsToTheAddress);

        if (correlationId is { } id)
            return correlationIdExists ? id : null;

        return openSendsToTheAddress.Count == 1 ? openSendsToTheAddress.Single() : null;
    }
}

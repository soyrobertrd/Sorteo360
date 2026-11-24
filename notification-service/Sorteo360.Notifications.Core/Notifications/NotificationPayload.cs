namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Los valores con los que se sustituye el correo de una persona, guardados solo mientras hacen
/// falta.
/// <para>
/// El envío sale fuera de la petición, así que entre el 202 y el socket tiene que quedar en algún
/// lado lo que el worker va a sustituir. Vive en su propia fila, no en el registro: la fila se
/// borra en el mismo momento en que el envío deja de ser reintentable —mandado, descartado o
/// fallido— y a partir de ahí ninguna de las cuatro tablas del historial contiene un valor de
/// <c>data</c>. Un token de registro que viaje acá dura lo que dura su envío.
/// </para>
/// <para>
/// Los valores llegan ya mezclados: los comunes del envío y los propios de la persona en un solo
/// diccionario plano. Mezclarlos acá es seguro porque una clave repetida entre los dos ya se
/// rechazó con un 400 antes de escribir nada.
/// </para>
/// </summary>
/// <param name="RecipientId">La fila de persona a la que pertenecen.</param>
/// <param name="Values">Los valores, planos y sin cascada.</param>
public sealed record NotificationPayload(Guid RecipientId, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>
    /// Los valores de una persona: los comunes del envío con los suyos encima, en un solo
    /// diccionario plano. No hay precedencia que resolver —una clave presente en los dos ya se
    /// rechazó con un 400 antes de escribir nada—, así que los dos conjuntos son disjuntos.
    /// </summary>
    public static NotificationPayload For(
        Guid recipientId,
        IReadOnlyDictionary<string, string> sharedData,
        IReadOnlyDictionary<string, string> recipientData)
    {
        ArgumentNullException.ThrowIfNull(sharedData);
        ArgumentNullException.ThrowIfNull(recipientData);

        var values = new Dictionary<string, string>(sharedData, StringComparer.Ordinal);

        foreach (var (key, value) in recipientData)
            values[key] = value;

        return new NotificationPayload(recipientId, values);
    }
}

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// La lista de la puerta tal como la leen las pantallas del borrador: reciente, no necesariamente
/// en vivo. Una pantalla de armado consulta mientras la gente llega, y una caída momentánea tiene
/// que degradarla en vez de dejarla en blanco (spec 01 §8).
/// <para>
/// Es un seam aparte de <see cref="IAttendanceSource"/> a propósito: arrancar el sorteo lee en vivo
/// por ese otro, sin saber que este existe, porque un bombo congelado de una copia vieja no tiene
/// arreglo.
/// </para>
/// </summary>
public interface IRecentAttendance
{
    /// <summary>
    /// La copia más reciente que haya de la lista de la puerta del evento. Nunca falla: sin ninguna
    /// copia contesta <see cref="AttendanceStatus.Unavailable"/> con la lista nula, que no es lo
    /// mismo que una lista vacía.
    /// </summary>
    Task<RecentAttendance> GetAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// La copia más reciente según <paramref name="requiresCheckIn"/> (A35/R2).
    /// </summary>
    Task<RecentAttendance> GetAsync(Guid eventId, bool requiresCheckIn, CancellationToken cancellationToken) =>
        GetAsync(eventId, cancellationToken);
}

/// <summary>
/// Lo que se sabe de la puerta: la lista, cuándo se obtuvo y qué tan vigente es. Con
/// <see cref="AttendanceStatus.Unavailable"/>, <paramref name="FetchedAt"/> y
/// <paramref name="Attendees"/> son nulos.
/// </summary>
public sealed record RecentAttendance(
    string Status,
    DateTimeOffset? FetchedAt,
    IReadOnlyList<Attendee>? Attendees);

/// <summary>
/// Qué tan vigente es la lista de la puerta que se sirvió. Es el mismo vocabulario que el de los
/// directorios del CRM, pero viaja en el cuerpo y no en <c>X-Crm-Directory-Status</c>: la
/// asistencia es de convocatorias, y esa cabecera habla solo por el CRM.
/// </summary>
public static class AttendanceStatus
{
    /// <summary>Salió de una copia vigente o recién leída.</summary>
    public const string Ok = "ok";

    /// <summary>La lectura falló y se sirvió la copia anterior, con su fecha original.</summary>
    public const string Stale = "stale";

    /// <summary>La lectura falló y nunca hubo copia: no se sabe quién está en la puerta.</summary>
    public const string Unavailable = "unavailable";
}

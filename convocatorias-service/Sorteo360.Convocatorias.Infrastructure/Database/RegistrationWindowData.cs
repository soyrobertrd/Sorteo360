namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>registration_window</c>: la puerta de inscripción de un evento (spec <c>06</c> §5.1).
/// <para>
/// Se escribe con el primer cierre y nunca antes. Una fila por cada evento que el servicio vio
/// sería el primer paso hacia una copia local de <c>Event</c>, y la segunda columna que alguien le
/// añadiera la convertiría en una. <b>Sin historial</b>: la fila guarda el último ciclo.
/// </para>
/// </summary>
internal sealed class RegistrationWindowData
{
    /// <summary>Clave primaria, y el mismo grano que <c>registration</c>.</summary>
    public Guid EventId { get; set; }

    /// <summary>Nula significa abierta. Es el estado entero.</summary>
    public DateTimeOffset? RegistrationsClosedAt { get; set; }

    /// <summary>Quién la cerró la última vez. Siempre puesta: la fila nace con un cierre.</summary>
    public Guid RegistrationsClosedById { get; set; }

    public DateTimeOffset? ReopenedAt { get; set; }

    public Guid? ReopenedById { get; set; }
}

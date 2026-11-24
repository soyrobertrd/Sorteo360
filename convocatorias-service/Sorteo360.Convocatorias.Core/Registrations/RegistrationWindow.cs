namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// Si un evento todavía acepta inscripciones nuevas (spec <c>06</c> §5). Una persona la cierra
/// cuando MITUR ya tiene gente suficiente; el evento sigue en pie y nadie recibe nada.
/// <para>
/// <b>No es el cierre del evento.</b> <c>EventStatus.closed</c> y <c>cerrar</c> significan que el
/// evento ya ocurrió, que es la situación contraria: acá el evento está por delante.
/// </para>
/// <para>
/// <b>La ausencia de fila significa abierta</b> (§5.1), y por eso <see cref="Open"/> existe: la
/// fila se escribe con el primer cierre y casi ningún evento la tiene nunca.
/// </para>
/// </summary>
/// <param name="EventId">El evento de events-service, sin clave foránea: vive en otra base.</param>
/// <param name="RegistrationsClosedAt">Cuándo se cerró. Nulo es abierta, y es el estado entero.</param>
/// <param name="RegistrationsClosedById">Quién la cerró la última vez.</param>
/// <param name="ReopenedAt">La reapertura más reciente, que se pisa en cada ciclo.</param>
/// <param name="ReopenedById">Quién la reabrió.</param>
public sealed record RegistrationWindow(
    Guid EventId,
    DateTimeOffset? RegistrationsClosedAt,
    Guid? RegistrationsClosedById,
    DateTimeOffset? ReopenedAt,
    Guid? ReopenedById)
{
    /// <summary>Si acepta inscripciones nuevas.</summary>
    public bool IsOpen => RegistrationsClosedAt is null;

    /// <summary>La ventana de un evento que no tiene fila: abierta y sin ningún sello.</summary>
    public static RegistrationWindow Open(Guid eventId) => new(eventId, null, null, null, null);
}

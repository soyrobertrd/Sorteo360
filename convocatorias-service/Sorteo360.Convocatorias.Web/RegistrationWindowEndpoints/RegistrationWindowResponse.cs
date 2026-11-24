namespace Sorteo360.Convocatorias.Web.RegistrationWindowEndpoints;

/// <summary>
/// El estado actual de la ventana de inscripción de un evento (spec <c>06</c> §5.2). Es la misma
/// forma para las dos rutas, y los nombres de <c>closedAt</c> / <c>closedById</c> son los que
/// <c>07</c> §2 pone en el resumen del evento.
/// </summary>
public sealed record RegistrationWindowResponse
{
    public Guid EventId { get; init; }

    /// <summary>Si acepta inscripciones nuevas. Un evento sin fila está abierto.</summary>
    public bool Open { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Quién la cerró la última vez. Se conserva al reabrir, como registro.</summary>
    public Guid? ClosedById { get; init; }

    /// <summary>La reapertura más reciente, pisada en cada ciclo.</summary>
    public DateTimeOffset? ReopenedAt { get; init; }

    public Guid? ReopenedById { get; init; }
}

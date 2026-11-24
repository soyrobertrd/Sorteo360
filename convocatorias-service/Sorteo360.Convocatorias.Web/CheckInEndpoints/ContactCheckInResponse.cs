namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Un evento al que asistió un contacto. Sin nombre ni lugar del evento: son de events-service, y
/// quien muestra la fila los resuelve allá por el id (CLAUDE.md raíz, ADR-0013).
/// </summary>
public sealed record ContactCheckInResponse
{
    public required Guid ContactId { get; init; }

    public required Guid EventId { get; init; }

    /// <summary>Cuándo entró: el día del evento en que estuvo ahí.</summary>
    public required DateTimeOffset CheckedInAt { get; init; }
}

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Una exclusión en lote tal como llega: exactamente una fuente, los ganadores vivos del sorteo de
/// <see cref="SourceEventId"/> o una lista de correos e IDs. DTO de entrada; la nota ya viene en
/// nulo si estaba en blanco.
/// </summary>
public sealed record BulkExcludeAttendeesDto(
    Guid EventId,
    Guid? SourceEventId,
    IReadOnlyList<string>? Identifiers,
    string? Note,
    Guid DecidedById);

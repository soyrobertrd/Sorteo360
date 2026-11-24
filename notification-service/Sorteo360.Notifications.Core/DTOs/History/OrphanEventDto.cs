namespace Sorteo360.Notifications.Core.DTOs.History;

/// <summary>
/// Un evento del proveedor que no se pudo correlacionar con ninguna fila de envío.
/// <para>
/// Tiene endpoint propio y no un filtro por id nulo: un huérfano no nombra ninguna fila, así que
/// no puede aparecer en el listado de personas ni aunque se quisiera; y muchos son rebotes, que es
/// el evento que más importa. Detrás de un filtro sería un cajón que nadie abre.
/// </para>
/// </summary>
public sealed record OrphanEventDto(
    Guid Id,
    string? SgEventId,
    string EventType,
    DateTimeOffset OccurredAt,
    string Payload);

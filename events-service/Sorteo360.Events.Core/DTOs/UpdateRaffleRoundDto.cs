namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// La representación editable completa de una ronda (ADR-0009): lo omitido llega en blanco y se
/// borra. El número no viaja: es del servicio. DTO de entrada: validado en el borde por
/// UpdateRaffleRoundRequestValidator.
/// </summary>
public record UpdateRaffleRoundDto(
    Guid EventId,
    Guid RoundId,
    string Name,
    string? Description,
    Guid UpdatedById);

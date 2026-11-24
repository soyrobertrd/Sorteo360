namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Datos para agregar una ronda al sorteo del evento. No lleva número: lo asigna el servicio.
/// DTO de entrada: validado en el borde por CreateRaffleRoundRequestValidator.
/// </summary>
public record CreateRaffleRoundDto(
    Guid EventId,
    string Name,
    string? Description,
    Guid CreatedById);

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Una decisión manual sobre un asistente, tal como llega: el correo sin normalizar —lo normaliza el
/// servicio antes de compararlo con nada— y la nota ya en nulo si venía en blanco. DTO de entrada:
/// validado en el borde por SetAttendeeOverrideRequestValidator.
/// </summary>
public record SetAttendeeOverrideDto(
    Guid EventId,
    string Email,
    string Decision,
    string? Note,
    Guid DecidedById);

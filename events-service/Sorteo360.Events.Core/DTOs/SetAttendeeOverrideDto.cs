namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Una decisión manual sobre un asistente, tal como llega: su clave (el correo, o la
/// <c>identityKey</c> de quien no tiene correo) sin normalizar —la normaliza el servicio antes de
/// compararla con nada— y la nota ya en nulo si venía en blanco. DTO de entrada:
/// validado en el borde por SetAttendeeOverrideRequestValidator.
/// </summary>
public record SetAttendeeOverrideDto(
    Guid EventId,
    string IdentityKey,
    string Decision,
    string? Note,
    Guid DecidedById);

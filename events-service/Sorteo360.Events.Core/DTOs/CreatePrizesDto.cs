namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Datos para agregar premios a una ronda del sorteo. <see cref="Quantity"/> es cuántos premios
/// idénticos crear, no una propiedad del premio: cinco bolsas son cinco premios con cinco ganadores.
/// DTO de entrada: validado en el borde por CreatePrizesRequestValidator.
/// </summary>
public record CreatePrizesDto(
    Guid EventId,
    Guid RoundId,
    string Name,
    string? Description,
    string? Value,
    Guid? SponsorCompanyId,
    Guid? SponsorContactId,
    int Quantity,
    Guid CreatedById,
    string? SponsorName = null);

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// La representación editable completa de un premio (ADR-0009): lo omitido llega en blanco y se
/// borra. La ronda es parte de ella, así que editarlo puede moverlo a otra ronda del mismo sorteo.
/// No hay cantidad: una vez creado, cada premio es uno. DTO de entrada: validado en el borde por
/// UpdatePrizeRequestValidator.
/// </summary>
public record UpdatePrizeDto(
    Guid EventId,
    Guid PrizeId,
    Guid RoundId,
    string Name,
    string? Description,
    string? Value,
    Guid? SponsorCompanyId,
    Guid? SponsorContactId,
    Guid UpdatedById,
    string? SponsorName = null);

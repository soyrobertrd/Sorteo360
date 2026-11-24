namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// La anulación del ganador vivo de un premio, con la nota ya recortada y en nulo si venía en blanco.
/// DTO de entrada: validado en el borde por VoidPrizeDrawRequestValidator.
/// </summary>
public record VoidPrizeDrawDto(
    Guid EventId,
    Guid PrizeId,
    string Code,
    string? Note,
    Guid VoidedById);

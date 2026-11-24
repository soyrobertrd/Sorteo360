namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// La reversión de la entrega viva de un premio, con el motivo ya recortado (spec 03 §3.3). DTO de
/// entrada: validado en el borde por ReversePrizeHandoverRequestValidator.
/// </summary>
public record ReversePrizeHandoverDto(
    Guid EventId,
    Guid PrizeId,
    string Reason,
    Guid ReversedById);

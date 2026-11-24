using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// La entrega de un premio con su firma, en una sola llamada (spec 03 §3.1, §3.2). Los campos llegan
/// ya recortados. DTO de entrada: validado en el borde por HandOverPrizeRequestValidator.
/// </summary>
public record HandOverPrizeDto(
    Guid EventId,
    Guid PrizeId,
    PrizeReceiver Receiver,
    Stream Signature,
    string SignatureFileName,
    string SignatureContentType,
    Guid RedeemedById);

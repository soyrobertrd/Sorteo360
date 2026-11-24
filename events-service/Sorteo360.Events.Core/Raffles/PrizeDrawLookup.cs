namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que el mostrador encuentra con un código de retiro (spec 03 §2.4): el sorteo con sus entregas, y
/// de qué evento, sorteo y premio es, porque quien busca no lo sabe.
/// </summary>
public sealed record PrizeDrawLookup(PrizeDraw Draw, Guid EventId, Guid RaffleId, string PrizeName);

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Un sorteo en la lista que cruza eventos: de qué evento es, en qué estado está y cuánto se sorteó
/// y se entregó. El nombre y la fecha del evento se leen del evento al consultar, nunca se copian al
/// sorteo (spec 00 §12). <c>DrawnCount</c> cuenta los premios con un ganador vivo, entregados
/// incluidos; <c>RedeemedCount</c>, los que además tienen una entrega en pie.
/// </summary>
public sealed record RaffleListItem(
    Guid RaffleId,
    Guid EventId,
    string EventName,
    DateOnly EventStartDate,
    string State,
    int PrizeCount,
    int DrawnCount,
    int RedeemedCount,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

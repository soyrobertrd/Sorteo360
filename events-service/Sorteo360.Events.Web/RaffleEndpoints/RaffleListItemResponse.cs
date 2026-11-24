using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Un sorteo en <c>GET /v1/raffles</c>: su evento, su estado y cuánto se sorteó y entregó.
/// <c>DrawnCount</c> cuenta los premios con un ganador vivo, entregados incluidos.
/// </summary>
public record RaffleListItemResponse
{
    public Guid RaffleId { get; init; }
    public Guid EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public DateOnly EventStartDate { get; init; }

    /// <summary><c>draft</c>, <c>in_progress</c> o <c>completed</c>.</summary>
    public string State { get; init; } = RaffleState.Draft;

    public int PrizeCount { get; init; }
    public int DrawnCount { get; init; }
    public int RedeemedCount { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }

    public static RaffleListItemResponse From(RaffleListItem item) => new()
    {
        RaffleId = item.RaffleId,
        EventId = item.EventId,
        EventName = item.EventName,
        EventStartDate = item.EventStartDate,
        State = item.State,
        PrizeCount = item.PrizeCount,
        DrawnCount = item.DrawnCount,
        RedeemedCount = item.RedeemedCount,
        StartedAt = item.StartedAt,
        CompletedAt = item.CompletedAt,
    };
}

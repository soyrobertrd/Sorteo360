namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Lote de hasta 10 premios a sortear en una sola transacción (A33).
/// </summary>
public sealed record DrawBatchPrizesRequest
{
    public IReadOnlyList<Guid> PrizeIds { get; init; } = [];
}


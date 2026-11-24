namespace Sorteo360.Events.Web.RaffleEndpoints;

public sealed record ImportPrizeItemRequest
{
    public int? RoundNumber { get; init; }
    public string? Name { get; init; }
    public string? Value { get; init; }
    public int? Quantity { get; init; }

    /// <summary>El patrocinador como texto libre; opcional. En blanco cuenta como sin patrocinador.</summary>
    public string? SponsorName { get; init; }
}

public sealed record ImportPrizesRequest
{
    public IReadOnlyList<ImportPrizeItemRequest>? Prizes { get; init; }
}

public sealed record ImportPrizesResponse(int Created, int RoundsCreated);


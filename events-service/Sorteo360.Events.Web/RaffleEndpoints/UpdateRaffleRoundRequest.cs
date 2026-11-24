namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// La representación editable completa de una ronda (ADR-0009). La descripción omitida se borra.
/// </summary>
public record UpdateRaffleRoundRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

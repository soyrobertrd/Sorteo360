namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>Una ronda nueva. El número no viaja: lo asigna el servicio.</summary>
public record CreateRaffleRoundRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

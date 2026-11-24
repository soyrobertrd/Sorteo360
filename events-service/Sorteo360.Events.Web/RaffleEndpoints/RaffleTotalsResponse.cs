using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>Las cifras de <c>GET /v1/raffles/totals</c>.</summary>
public record RaffleTotalsResponse
{
    public int Raffles { get; init; }

    /// <summary>Los sorteos en curso.</summary>
    public int Active { get; init; }

    public int Prizes { get; init; }

    /// <summary>Los sorteos de premio vivos.</summary>
    public int Winners { get; init; }

    /// <summary>Los vivos con una entrega en pie.</summary>
    public int Redeemed { get; init; }

    /// <summary>Los calificados de los bombos congelados; un borrador no aporta.</summary>
    public int QualifiedParticipants { get; init; }

    public static RaffleTotalsResponse From(RaffleTotals totals) => new()
    {
        Raffles = totals.Raffles,
        Active = totals.Active,
        Prizes = totals.Prizes,
        Winners = totals.Winners,
        Redeemed = totals.Redeemed,
        QualifiedParticipants = totals.QualifiedParticipants,
    };
}

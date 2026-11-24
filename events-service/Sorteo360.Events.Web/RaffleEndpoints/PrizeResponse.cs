using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Un premio. El valor es texto tal como lo escribió el operador, no un monto.
/// </summary>
public record PrizeResponse
{
    public Guid Id { get; init; }
    public Guid RoundId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Value { get; init; }
    public string? ImageUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>La empresa patrocinadora y su contacto, o <c>null</c> si el premio no tiene.</summary>
    public PrizeSponsorResponse? Sponsor { get; init; }

    /// <summary>
    /// <c>undrawn</c>, <c>drawn</c> o <c>redeemed</c>. No se guarda: sale de <see cref="Draws"/> y de sus
    /// entregas. Un premio con todos sus sorteos anulados vuelve a <c>undrawn</c>, y uno cuya entrega se
    /// revirtió vuelve a <c>drawn</c>.
    /// </summary>
    public string State { get; init; } = PrizeState.Undrawn;

    /// <summary>
    /// Sus sorteos, vivos y anulados, del más nuevo al más viejo. El ganador es el que tiene
    /// <c>void</c> nulo. Cada uno con su código de retiro, su estado y sus entregas. Sin correo.
    /// </summary>
    public IReadOnlyList<PrizeDrawSummary> Draws { get; init; } = [];

    /// <summary>Un premio recién creado: sin sorteos y sin logo, que se sube después.</summary>
    public static PrizeResponse From(Prize prize) => From(prize, [], RaffleSponsorNames.None, sponsorLogo: null);

    public static PrizeResponse From(PrizeWithDraws prize, IReadOnlyDictionary<Guid, string?>? operatorNames = null) =>
        From(prize.Prize, prize.Draws, RaffleSponsorNames.None, prize.SponsorLogo, operatorNames);

    /// <summary>El premio con sus sorteos, que llegan ya del más nuevo al más viejo.</summary>
    public static PrizeResponse From(
        Prize prize,
        IReadOnlyList<PrizeDraw> draws,
        RaffleSponsorNames sponsorNames,
        StoredFile? sponsorLogo,
        IReadOnlyDictionary<Guid, string?>? operatorNames = null) => new()
    {
        Id = prize.Id,
        RoundId = prize.RoundId,
        Name = prize.Name,
        Description = prize.Description,
        Value = prize.Value,
        ImageUrl = prize.ImageStoredFileId is { } imgId ? $"/v1/files/{imgId}/content" : null,
        CreatedAt = prize.CreatedAt,
        Sponsor = PrizeSponsorResponse.From(prize, sponsorNames, sponsorLogo),
        State = PrizeState.Of(draws),
        Draws = draws.Select(d => PrizeDrawSummary.From(d, operatorNames)).ToList(),
    };
}

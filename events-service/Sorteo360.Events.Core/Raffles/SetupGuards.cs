using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que del armado deja de poder tocarse una vez que hay sorteos (spec 00 §4). Cuenta cualquier
/// sorteo, vivo o anulado: los dos los nombra el acta. Se evalúan bajo el bloqueo del sorteo sobre
/// los sorteos releídos (ADR-0028), así que una edición que compite con un sorteo entra antes o se
/// rechaza.
/// </summary>
public static class SetupGuards
{
    /// <summary><c>PRIZE_HAS_DRAWS</c> si el premio tiene algún sorteo.</summary>
    public static UnitResult<BaseError> EnsurePrizeHasNoDraws(Guid prizeId, IReadOnlyList<PrizeDraw> raffleDraws)
    {
        ArgumentNullException.ThrowIfNull(raffleDraws);

        return raffleDraws.Any(d => d.PrizeId == prizeId)
            ? UnitResult.Failure(RaffleErrors.PrizeHasDraws())
            : UnitResult.Success<BaseError>();
    }

    /// <summary>
    /// Subir el logo del patrocinador: el premio queda fijo una vez sorteado, como el resto de
    /// su armado (<c>PRIZE_HAS_DRAWS</c>), y el logo es de su empresa patrocinadora, así que sin ella es
    /// <c>PRIZE_NOT_SPONSORED</c>. En ese orden: lo que ya no se puede tocar se dice antes que lo que
    /// falta.
    /// </summary>
    public static UnitResult<BaseError> EnsureCanSetSponsorLogo(Prize prize, IReadOnlyList<PrizeDraw> raffleDraws)
    {
        ArgumentNullException.ThrowIfNull(prize);

        var noDraws = EnsurePrizeHasNoDraws(prize.Id, raffleDraws);
        if (noDraws.IsFailure)
            return noDraws;

        return prize.SponsorCompanyId is null && string.IsNullOrWhiteSpace(prize.SponsorName)
            ? UnitResult.Failure(RaffleErrors.PrizeNotSponsored())
            : UnitResult.Success<BaseError>();
    }

    /// <summary>
    /// <c>PRIZE_HAS_DRAWS</c>, con la prosa de la ronda, si alguno de sus premios tiene algún sorteo.
    /// </summary>
    public static UnitResult<BaseError> EnsureRoundHasNoDraws(IReadOnlyCollection<Guid> roundPrizeIds, IReadOnlyList<PrizeDraw> raffleDraws)
    {
        ArgumentNullException.ThrowIfNull(roundPrizeIds);
        ArgumentNullException.ThrowIfNull(raffleDraws);

        return raffleDraws.Any(d => roundPrizeIds.Contains(d.PrizeId))
            ? UnitResult.Failure(RaffleErrors.RoundHasDraws())
            : UnitResult.Success<BaseError>();
    }
}

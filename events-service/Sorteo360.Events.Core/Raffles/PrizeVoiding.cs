using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que decide la anulación de un premio bajo el bloqueo del sorteo (spec 02 §3): si se puede y cómo
/// queda el sorteo anulado. Puro, como <see cref="PrizeDrawing"/>.
/// </summary>
public static class PrizeVoiding
{
    /// <summary>
    /// La anulación del ganador vivo del premio, sobre los sorteos tal como se leen bajo el bloqueo. En
    /// este orden: <c>RAFFLE_NOT_IN_PROGRESS</c> o <c>RAFFLE_COMPLETED</c> según el estado, y
    /// <c>PRIZE_NOT_DRAWN</c> si el premio no tiene un sorteo vivo, y <c>PRIZE_ALREADY_REDEEMED</c> si
    /// ese sorteo tiene una entrega viva. Devuelve ese sorteo con el motivo,
    /// la nota, cuándo y quién; el resto, el código de retiro incluido, no cambia.
    /// </summary>
    public static Result<PrizeDraw, BaseError> Void(
        Raffle raffle,
        Prize prize,
        IReadOnlyList<PrizeDraw> raffleDraws,
        string code,
        string? note,
        Guid voidedById,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(raffle);
        ArgumentNullException.ThrowIfNull(prize);
        ArgumentNullException.ThrowIfNull(raffleDraws);
        ArgumentNullException.ThrowIfNull(code);

        var inProgress = raffle.EnsureInProgress();
        if (inProgress.IsFailure)
            return Result.Failure<PrizeDraw, BaseError>(inProgress.Error);

        var liveDraw = raffleDraws.FirstOrDefault(d => d.PrizeId == prize.Id && d.IsLive);
        if (liveDraw is null)
            return Result.Failure<PrizeDraw, BaseError>(RaffleErrors.PrizeNotDrawn());

        // Un premio entregado no se anula: lo único que lo devuelve es la reversión, que dice por qué
        // (spec 03 §3.4).
        if (liveDraw.LiveHandover is not null)
            return Result.Failure<PrizeDraw, BaseError>(RaffleErrors.PrizeAlreadyRedeemed());

        return liveDraw with
        {
            VoidCode = code,
            VoidNote = note,
            VoidedAt = now,
            VoidedById = voidedById,
        };
    }
}

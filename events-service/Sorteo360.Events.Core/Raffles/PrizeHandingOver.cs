using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que deciden la entrega de un premio y su reversión bajo el bloqueo del sorteo (spec 03 §3.4).
/// Puro, como <see cref="PrizeVoiding"/>. Ninguna de las dos mira el estado del evento, y las dos
/// admiten un sorteo completado: los ganadores retiran días después.
/// </summary>
public static class PrizeHandingOver
{
    /// <summary>
    /// La entrega del premio sobre su sorteo vivo, con los sorteos tal como se leen bajo el bloqueo.
    /// En este orden: <c>RAFFLE_NOT_IN_PROGRESS</c> en draft, <c>PRIZE_NOT_DRAWN</c> sin sorteo vivo y
    /// <c>PRIZE_ALREADY_REDEEMED</c> si ese sorteo ya tiene una entrega viva.
    /// </summary>
    public static Result<PrizeHandover, BaseError> HandOver(
        Raffle raffle,
        Prize prize,
        IReadOnlyList<PrizeDraw> raffleDraws,
        PrizeReceiver receiver,
        Guid signatureStoredFileId,
        Guid redeemedById,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(raffle);
        ArgumentNullException.ThrowIfNull(prize);
        ArgumentNullException.ThrowIfNull(raffleDraws);
        ArgumentNullException.ThrowIfNull(receiver);

        var started = raffle.EnsureStarted();
        if (started.IsFailure)
            return Result.Failure<PrizeHandover, BaseError>(started.Error);

        var liveDraw = raffleDraws.FirstOrDefault(d => d.PrizeId == prize.Id && d.IsLive);
        if (liveDraw is null)
            return Result.Failure<PrizeHandover, BaseError>(RaffleErrors.PrizeNotDrawn());

        if (liveDraw.LiveHandover is not null)
            return Result.Failure<PrizeHandover, BaseError>(RaffleErrors.PrizeAlreadyRedeemed());

        return new PrizeHandover(
            Id: Guid.NewGuid(),
            PrizeDrawId: liveDraw.Id,
            ReceiverName: receiver.Name,
            ReceiverIdentityDocument: receiver.IdentityDocument,
            ReceiverIsWinner: receiver.IsWinner,
            SignatureStoredFileId: signatureStoredFileId,
            RedeemedAt: now,
            RedeemedById: redeemedById,
            ReversedAt: null,
            ReversedById: null,
            ReversalReason: null);
    }

    /// <summary>
    /// La reversión de la entrega viva del sorteo vivo del premio. <c>RAFFLE_NOT_IN_PROGRESS</c> en
    /// draft y <c>PRIZE_NOT_REDEEMED</c> sin entrega viva. Devuelve esa entrega con cuándo, quién y
    /// por qué; el resto, la firma incluida, no cambia (spec 03 §3.3).
    /// </summary>
    public static Result<PrizeHandover, BaseError> Reverse(
        Raffle raffle,
        Prize prize,
        IReadOnlyList<PrizeDraw> raffleDraws,
        string reason,
        Guid reversedById,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(raffle);
        ArgumentNullException.ThrowIfNull(prize);
        ArgumentNullException.ThrowIfNull(raffleDraws);
        ArgumentNullException.ThrowIfNull(reason);

        var started = raffle.EnsureStarted();
        if (started.IsFailure)
            return Result.Failure<PrizeHandover, BaseError>(started.Error);

        var liveHandover = raffleDraws
            .FirstOrDefault(d => d.PrizeId == prize.Id && d.IsLive)?
            .LiveHandover;
        if (liveHandover is null)
            return Result.Failure<PrizeHandover, BaseError>(RaffleErrors.PrizeNotRedeemed());

        return liveHandover with
        {
            ReversedAt = now,
            ReversedById = reversedById,
            ReversalReason = reason,
        };
    }
}

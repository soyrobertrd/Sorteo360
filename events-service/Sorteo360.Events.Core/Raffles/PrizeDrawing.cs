using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que decide el sorteo de un premio bajo el bloqueo del sorteo (spec 02 §2): si se puede, a quién
/// le toca y qué queda escrito. Puro: no lee nada, no mira la hora y no elige al azar por su cuenta.
/// </summary>
public static class PrizeDrawing
{
    /// <summary>
    /// El sorteo del premio, sobre el sorteo, los asistentes congelados y los sorteos tal como se leen
    /// bajo el bloqueo. En este orden: <c>RAFFLE_NOT_IN_PROGRESS</c> o <c>RAFFLE_COMPLETED</c> según
    /// el estado, <c>PRIZE_ALREADY_DRAWN</c> si el premio ya tiene uno vivo, y
    /// <c>RAFFLE_POOL_EXHAUSTED</c> si no queda nadie.
    /// </summary>
    /// <param name="pick">
    /// Dado el tamaño del bombo, el índice del ganador. En producción es
    /// <c>RandomNumberGenerator.GetInt32</c>; un test pasa uno fijo.
    /// </param>
    public static Result<PrizeDraw, BaseError> Draw(
        Raffle raffle,
        Prize prize,
        IReadOnlyList<RaffleAttendee> attendees,
        IReadOnlyList<PrizeDraw> raffleDraws,
        Func<int, int> pick,
        Guid drawnById,
        DateTimeOffset now,
        string redemptionCode)
    {
        ArgumentNullException.ThrowIfNull(raffle);
        ArgumentNullException.ThrowIfNull(prize);
        ArgumentNullException.ThrowIfNull(raffleDraws);
        ArgumentNullException.ThrowIfNull(pick);
        ArgumentNullException.ThrowIfNull(redemptionCode);

        var inProgress = raffle.EnsureInProgress();
        if (inProgress.IsFailure)
            return Result.Failure<PrizeDraw, BaseError>(inProgress.Error);

        var liveDraw = raffleDraws.FirstOrDefault(d => d.PrizeId == prize.Id && d.IsLive);
        if (liveDraw is not null)
            return Result.Failure<PrizeDraw, BaseError>(new PrizeAlreadyDrawnError(liveDraw));

        var pool = EligiblePool.For(attendees, raffleDraws, raffle.OnePrizePerCompany);
        if (pool.Count == 0)
            return Result.Failure<PrizeDraw, BaseError>(new PoolExhaustedError(pool));

        var winner = pool.Members[pick(pool.Count)];
        return new PrizeDraw(
            Id: Guid.NewGuid(),
            PrizeId: prize.Id,
            WinnerAttendeeId: winner.Id,
            WinnerName: winner.DisplayName,
            WinnerCompany: winner.Company,
            WinnerEmail: winner.EmailNormalized,
            DrawnAt: now,
            DrawnById: drawnById,
            EligiblePoolSize: pool.Count,
            OnePrizePerPerson: true,
            OnePrizePerCompany: raffle.OnePrizePerCompany,
            RedemptionCode: redemptionCode,
            VoidCode: null,
            VoidNote: null,
            VoidedAt: null,
            VoidedById: null);
    }
}

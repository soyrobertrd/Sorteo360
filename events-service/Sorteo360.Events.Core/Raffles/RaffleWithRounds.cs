using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Files;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// El sorteo con sus rondas, ordenadas por número, y los premios de cada una, ordenados por
/// creación y luego por identificador: las cinco bolsas de un mismo alta comparten el instante.
/// <para>
/// Es donde viven las reglas del armado —qué se puede tocar según el estado del sorteo, qué ronda o
/// premio es de este sorteo y cuándo una ronda puede eliminarse—. Todas leen lo que el agregado ya
/// tiene en la mano, así que un test las ejercita armándolo. El estado del evento queda afuera: lo
/// comprueba <c>RaffleService</c>, que puede leerlo.
/// </para>
/// </summary>
public sealed record RaffleWithRounds
{
    public Raffle Raffle { get; init; }
    public IReadOnlyList<RaffleRound> Rounds { get; init; }

    /// <summary>
    /// Todos los sorteos de todos los premios, vivos y anulados, sin orden. De ellos sale el estado
    /// de cada premio y su historia (spec 02 §2), y el bombo de cualquiera (spec 02 §1).
    /// </summary>
    public IReadOnlyList<PrizeDraw> Draws { get; init; } = [];

    /// <summary>
    /// Las versiones del acta, de la más nueva a la más vieja (spec 03 §4.6). Vacía hasta completar.
    /// </summary>
    public IReadOnlyList<NotarialRecord> NotarialRecords { get; init; } = [];

    /// <summary>
    /// La ficha del archivo de cada logo de patrocinador enlazado, sin orden. Un archivo que el
    /// almacenamiento perdió sigue acá, con su <c>MissingSince</c>.
    /// </summary>
    public IReadOnlyList<StoredFile> SponsorLogos { get; init; } = [];

    /// <summary>
    /// Asistentes con al menos una anulación que fueron reactivados para volver al bombo.
    /// </summary>
    public IReadOnlyList<Guid> ReinstatedAttendeeIds { get; init; } = [];

    public RaffleWithRounds(Raffle raffle, IReadOnlyList<RaffleRound> rounds)
    {
        ArgumentNullException.ThrowIfNull(raffle);
        ArgumentNullException.ThrowIfNull(rounds);

        Raffle = raffle;
        Rounds = rounds
            .OrderBy(r => r.Number)
            .Select(r => r with
            {
                Prizes = r.Prizes.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).ToList()
            })
            .ToList();
    }

    /// <summary>
    /// Los sorteos de un premio, del más nuevo al más viejo: después de un nuevo sorteo, la historia
    /// es lo que muestra la pantalla del operador.
    /// </summary>
    public IReadOnlyList<PrizeDraw> DrawsOf(Guid prizeId) => Draws
        .Where(d => d.PrizeId == prizeId)
        .OrderByDescending(d => d.DrawnAt)
        .ThenByDescending(d => d.Id)
        .ToList();

    /// <summary>El archivo del logo del patrocinador del premio, o <c>null</c> si no tiene.</summary>
    public StoredFile? SponsorLogoOf(Prize prize)
    {
        ArgumentNullException.ThrowIfNull(prize);

        return prize.SponsorLogoFileId is { } fileId ? SponsorLogos.FirstOrDefault(f => f.Id == fileId) : null;
    }

    /// <summary>
    /// El número que le toca a la próxima ronda. Es lo que ve este agregado; el que se guarda lo
    /// fija el repositorio bajo el bloqueo del sorteo, por si otra ronda entró en el medio.
    /// </summary>
    public int NextRoundNumber => Rounds.Count == 0 ? 1 : Rounds.Max(r => r.Number) + 1;

    /// <summary>
    /// Exige que el armado —rondas y premios— admita cambios: en draft y en in_progress sí, en un
    /// sorteo completado no, porque el acta ya fijó lo que atestigua.
    /// </summary>
    public UnitResult<BaseError> EnsureCanChangeSetup() => Raffle.State == RaffleState.Completed
        ? UnitResult.Failure(RaffleErrors.Completed())
        : UnitResult.Success<BaseError>();

    /// <summary>La ronda indicada, o <c>ROUND_NOT_FOUND</c> si no es de este sorteo.</summary>
    public Result<RaffleRound, BaseError> FindRound(Guid roundId)
    {
        var round = Rounds.FirstOrDefault(r => r.Id == roundId);
        return round is null
            ? Result.Failure<RaffleRound, BaseError>(RaffleErrors.RoundNotFound())
            : Result.Success<RaffleRound, BaseError>(round);
    }

    /// <summary>El premio indicado, en cualquier ronda, o <c>PRIZE_NOT_FOUND</c> si no es de este sorteo.</summary>
    public Result<Prize, BaseError> FindPrize(Guid prizeId)
    {
        var prize = Rounds.SelectMany(r => r.Prizes).FirstOrDefault(p => p.Id == prizeId);
        return prize is null
            ? Result.Failure<Prize, BaseError>(RaffleErrors.PrizeNotFound())
            : Result.Success<Prize, BaseError>(prize);
    }

    /// <summary>
    /// El premio indicado, listo para ir a la ronda indicada: los dos tienen que ser de este sorteo.
    /// Moverlo a una ronda ajena sería sacarlo del sorteo por la puerta de atrás. El premio se busca
    /// primero, así que uno ajeno responde <c>PRIZE_NOT_FOUND</c> aunque la ronda también lo sea.
    /// </summary>
    public Result<Prize, BaseError> EnsureCanMovePrize(Guid prizeId, Guid roundId)
    {
        var prize = FindPrize(prizeId);
        if (prize.IsFailure)
            return prize;

        var round = FindRound(roundId);
        return round.IsFailure ? Result.Failure<Prize, BaseError>(round.Error) : prize;
    }

    /// <summary>
    /// Eliminar una ronda, solo si está vacía. Sus premios no se van en cascada: cuáles se mueven y
    /// cuáles se eliminan lo decide el operador.
    /// </summary>
    public UnitResult<BaseError> EnsureCanDeleteRound(Guid roundId)
    {
        var round = FindRound(roundId);
        if (round.IsFailure)
            return UnitResult.Failure(round.Error);

        return round.Value.Prizes.Count > 0
            ? UnitResult.Failure(RaffleErrors.RoundNotEmpty())
            : UnitResult.Success<BaseError>();
    }
}

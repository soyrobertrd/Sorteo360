using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que escribe el arranque: el sorteo en curso y una fila por cada persona en la puerta (spec 01
/// §4). Es lo único que el repositorio guarda, y lo guarda entero o nada.
/// </summary>
public sealed record RaffleFreeze(Raffle Started, IReadOnlyList<RaffleAttendee> Attendees)
{
    /// <summary>
    /// El congelado, decidido sobre el sorteo y las decisiones manuales tal como se leen bajo el
    /// bloqueo de su fila, y sobre la puerta, la lista de DO Travel Awards y la respuesta del CRM
    /// leídas en vivo antes de tomarlo. Puro: no lee nada ni mira la hora.
    /// <para>
    /// <c>RAFFLE_NOT_DRAFT</c> si otro arranque llegó antes; <c>RAFFLE_NO_PARTICIPANTS</c> si el
    /// bombo queda vacío, sea porque nadie llegó, porque todos quedaron fuera o porque la lista de
    /// DO Travel Awards no coincide con nadie.
    /// </para>
    /// </summary>
    public static Result<RaffleFreeze, BaseError> Create(
        Raffle locked,
        IReadOnlyList<Attendee> door,
        Maybe<AwardsList> awardsList,
        IReadOnlyCollection<AttendeeOverride> overrides,
        IReadOnlyDictionary<Guid, string?> mailability,
        Guid startedById,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(locked);
        ArgumentNullException.ThrowIfNull(door);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(mailability);

        var batchId = awardsList.HasValue ? awardsList.Value.BatchId : (Guid?)null;
        var started = locked.Start(batchId, startedById, now);
        if (started.IsFailure)
            return Result.Failure<RaffleFreeze, BaseError>(started.Error);

        var classified = Qualification.Classify(door, awardsList, overrides, Maybe.From(mailability));
        if (classified.Qualified == 0)
            return Result.Failure<RaffleFreeze, BaseError>(RaffleErrors.NoParticipants());

        return new RaffleFreeze(started.Value, classified.Attendees.Select(RaffleAttendee.From).ToList());
    }
}

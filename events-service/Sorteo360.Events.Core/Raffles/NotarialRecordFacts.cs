using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Todo lo que el acta afirma, leído bajo el bloqueo del sorteo en la misma transacción que lo
/// completa (spec 03 §4.4): el sorteo ya completado con sus rondas, premios y sorteos, la lista
/// congelada de la puerta y el evento. Un sorteo o una anulación no pueden colarse entre la lectura
/// y el documento.
/// </summary>
public sealed record NotarialRecordFacts(
    RaffleWithRounds Raffle,
    IReadOnlyList<RaffleAttendee> Attendees,
    NotarialRecordEvent Event)
{
    /// <summary>
    /// Cada operador que el acta nombra, una vez: quien tomó una decisión manual (spec 03 §4.4 paso
    /// 4). Nadie más, porque cada id es una llamada al realm con la fila del sorteo bloqueada.
    /// </summary>
    public IReadOnlyCollection<Guid> OperatorIds() => Attendees
        .SelectMany(a => new[] { a.ManualDecidedById, a.ReinstatedById })
        .OfType<Guid>()
        .Distinct()
        .ToList();
}

/// <summary>
/// El evento tal como lo dice el acta: nombre, fechas y dónde fue. Un evento tiene una ciudad; si
/// los datos todavía traen más de una viva, van todas en su orden de pantalla y ninguna se calla.
/// </summary>
public sealed record NotarialRecordEvent(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<NotarialRecordPlace> Places,
    string? OrganizerName = null);

/// <summary>
/// Una ciudad del evento con su país, tal como están guardados, y su zona horaria: la hora en la que
/// el acta escribe sus momentos si esta es la primera ciudad.
/// </summary>
public sealed record NotarialRecordPlace(string City, string Country, CityTimeZone TimeZone);

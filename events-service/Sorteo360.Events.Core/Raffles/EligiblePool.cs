namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Quién puede ganar un premio en este momento (spec 02 §1), y por qué quedó afuera el resto. Es una
/// sola función con dos usos: la lectura que alimenta la ruleta y el sorteo bajo el bloqueo, así que
/// la lista que el operador ve y la que decide son la misma.
/// <para>
/// Las cuatro cuentas reparten a los calificados sin repetir: cada uno cae en la primera línea que lo
/// saca, así que con <see cref="Members"/> suman <see cref="Participants"/>.
/// </para>
/// </summary>
/// <param name="Members">Los que pueden ganar, ordenados por id para que un índice signifique lo mismo en cada corrida.</param>
/// <param name="Participants">Los calificados del bombo congelado.</param>
/// <param name="ExcludedByVoid">Fuera por una anulación propia en cualquier premio del sorteo, con cualquier motivo.</param>
/// <param name="ExcludedByPerson">Fuera por tener ya un premio vivo, con la regla de un premio por persona.</param>
/// <param name="ExcludedByCompany">Fuera porque su empresa ya tiene un premio vivo, con la regla de uno por empresa.</param>
/// <param name="ExcludedByLinkedRaffle">Fuera por tener un premio vivo en un sorteo vinculado.</param>
public sealed record EligiblePool(
    IReadOnlyList<RaffleAttendee> Members,
    int Participants,
    int ExcludedByVoid,
    int ExcludedByPerson,
    int ExcludedByCompany,
    int ExcludedByLinkedRaffle = 0)
{
    public int Count => Members.Count;

    /// <summary>
    /// El bombo del premio: los calificados menos, en este orden, quien tiene un sorteo anulado en
    /// cualquier premio del sorteo, quien ya tiene un premio vivo (siempre: quien gana queda fuera de
    /// los premios siguientes de la ronda y del sorteo, sin interruptor que lo apague), y quien
    /// comparte empresa con un premio vivo si rige uno por empresa. Solo los sorteos vivos cuentan
    /// para las dos reglas: un ganador anulado no se llevó nada. Se pide por premio porque es de lo que
    /// sortea ese premio; con las reglas de hoy, todos los premios sin sortear tienen el mismo bombo.
    /// </summary>
    public static EligiblePool For(
        IReadOnlyList<RaffleAttendee> attendees,
        IReadOnlyList<PrizeDraw> raffleDraws,
        bool onePrizePerCompany)
    {
        ArgumentNullException.ThrowIfNull(attendees);
        ArgumentNullException.ThrowIfNull(raffleDraws);

        var liveWinners = new HashSet<Guid>();
        var liveCompanies = onePrizePerCompany ? new HashSet<string>(StringComparer.Ordinal) : null;
        // R12: una sola pasada O(sorteos) para la última anulación por asistente, evitando O(asistentes × sorteos).
        var latestVoidByAttendee = new Dictionary<Guid, DateTimeOffset>();

        foreach (var draw in raffleDraws)
        {
            if (draw.IsLive)
            {
                liveWinners.Add(draw.WinnerAttendeeId);
                if (liveCompanies is not null)
                {
                    var key = CompanyKey.Normalize(draw.WinnerCompany);
                    if (key.HasValue)
                        liveCompanies.Add(key.Value);
                }
            }
            else if (draw.VoidedAt is { } voidedAt)
            {
                if (!latestVoidByAttendee.TryGetValue(draw.WinnerAttendeeId, out var prev) || voidedAt > prev)
                    latestVoidByAttendee[draw.WinnerAttendeeId] = voidedAt;
            }
        }

        var members = new List<RaffleAttendee>(attendees.Count);
        int participants = 0, byVoid = 0, byPerson = 0, byCompany = 0, byLinked = 0;

        foreach (var attendee in attendees)
        {
            if (!attendee.Qualified)
                continue;

            participants++;

            var isVoided = latestVoidByAttendee.TryGetValue(attendee.Id, out var lastVoidedAt)
                && (attendee.ReinstatedAt is null || attendee.ReinstatedAt.Value < lastVoidedAt);

            if (isVoided)
                byVoid++;
            else if (liveWinners.Contains(attendee.Id))
                byPerson++;
            else if (attendee.WonInLinkedRaffle)
                byLinked++;
            else if (liveCompanies is not null && CompanyKey.Normalize(attendee.Company) is { HasValue: true } company && liveCompanies.Contains(company.Value))
                byCompany++;
            else
                members.Add(attendee);
        }

        members.Sort((a, b) => a.Id.CompareTo(b.Id));
        return new EligiblePool(members, participants, byVoid, byPerson, byCompany, byLinked);
    }
}

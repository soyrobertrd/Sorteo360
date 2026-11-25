using System.Globalization;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que dice un acta, en el orden de spec 03 §4.2 y como hechos sin redacción: listas ya ordenadas,
/// cuentas, nombres ya resueltos, códigos y horas en UTC. La redacción en español, las horas locales
/// y la maquetación son del renderizador (ADR-0024). Una reemisión arma otro con otra versión.
/// <para>
/// Lo que el acta no dice tampoco está acá: ni códigos de retiro, ni entregas, ni correos, ni la nota
/// de una decisión manual (spec 03 §4.3). Lo que no está en el contenido ningún renderizador lo puede
/// imprimir.
/// </para>
/// <para>
/// Las reglas son las de los sorteos, no las que el sorteo tiene al completarse: pueden cambiar en
/// curso (spec 02 §1.2). Si todos los sorteos salieron bajo las mismas, <see cref="OnePrizePerPerson"/>
/// y <see cref="OnePrizePerCompany"/> son esas; si no, <see cref="RulesChanged"/> es verdadero y cada
/// línea del registro dice las suyas. Sin sorteos, son las guardadas en el sorteo (spec 03 §4.2
/// punto 4).
/// </para>
/// </summary>
public sealed record NotarialRecordContent(
    string RecordNumber,
    int Version,
    NotarialRecordReplaces? Replaces,
    NotarialRecordEvent Event,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool OnePrizePerPerson,
    bool OnePrizePerCompany,
    bool RulesChanged,
    bool AwardsListApplied,
    NotarialRecordArithmetic Arithmetic,
    IReadOnlyList<NotarialRecordDecision> ManualDecisions,
    IReadOnlyList<NotarialRecordRound> Rounds,
    IReadOnlyList<NotarialRecordDrawLine> DrawLog,
    RaffleAuthorities Authorities,
    IReadOnlyList<NotarialRecordParticipant> Annex)
{
    /// <summary>
    /// El anexo se ordena por apellido y nombre sin distinguir mayúsculas ni acentos: "Álvarez" va con
    /// las A y no detrás de la Z, como lo pondría una comparación ordinal.
    /// </summary>
    private static readonly StringComparer NameOrder =
        StringComparer.Create(CultureInfo.GetCultureInfo("es-DO"), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

    /// <summary>Cuántos premios tienen un ganador vivo.</summary>
    public int PrizesAwarded => Rounds.Sum(r => r.Prizes.Count(p => p.WinnerName is not null));

    /// <summary>Cuántos quedaron sin adjudicar, que el acta dice tal cual (spec 00 §2).</summary>
    public int PrizesNotAwarded => Rounds.Sum(r => r.Prizes.Count(p => p.WinnerName is null));

    /// <summary>Participantes reactivados tras una anulación para volver al bombo.</summary>
    public IReadOnlyList<NotarialRecordReinstatement> Reinstatements { get; init; } = [];

    /// <summary>Los sorteos vinculados: quien tenía un premio vivo en uno de ellos no entró al bombo.</summary>
    public IReadOnlyList<string> LinkedRaffleNames { get; init; } = [];

    /// <summary>
    /// El número del acta: los primeros ocho dígitos hexadecimales del id del sorteo, en mayúsculas.
    /// Los ids son aleatorios, así que cualquier prefijo lo es.
    /// </summary>
    public static string NumberOf(Guid raffleId) => raffleId.ToString("N")[..8].ToUpperInvariant();

    /// <summary>
    /// El contenido del acta a partir de lo leído bajo el bloqueo, los nombres de los operadores y los
    /// de las empresas patrocinadoras, por id. El sorteo tiene que venir completado: sus dos horas son
    /// las del acta. La versión 1 no reemplaza a ninguna, y toda versión posterior dice a cuál
    /// reemplaza (spec 03 §4.5).
    /// </summary>
    public static NotarialRecordContent Build(
        NotarialRecordFacts facts,
        IReadOnlyDictionary<Guid, string?> operatorNames,
        int version,
        NotarialRecordReplaces? replaces,
        IReadOnlyDictionary<Guid, string>? sponsorNames = null)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(operatorNames);
        sponsorNames ??= new Dictionary<Guid, string>();
        if ((version == NotarialRecord.FirstVersion) != (replaces is null))
            throw new ArgumentException(
                "La versión 1 no reemplaza a ninguna, y una posterior dice a cuál reemplaza.", nameof(replaces));

        var withRounds = facts.Raffle;
        var raffle = withRounds.Raffle;
        if (raffle.StartedAt is not { } startedAt || raffle.CompletedAt is not { } completedAt)
            throw new InvalidOperationException("El acta se arma con el sorteo ya arrancado y completado.");

        var drawLog = DrawLogOf(withRounds);
        var drawnUnder = drawLog.Select(l => (l.OnePrizePerPerson, l.OnePrizePerCompany)).Distinct().ToList();
        var (onePrizePerPerson, onePrizePerCompany) = drawnUnder.Count == 0
            ? (raffle.OnePrizePerPerson, raffle.OnePrizePerCompany)
            : drawnUnder[0];

        return new NotarialRecordContent(
            NumberOf(raffle.Id),
            version,
            replaces,
            facts.Event,
            startedAt,
            completedAt,
            onePrizePerPerson,
            onePrizePerCompany,
            RulesChanged: drawnUnder.Count > 1,
            AwardsListApplied: raffle.AwardsBatchId is not null,
            ArithmeticOf(facts.Attendees),
            DecisionsOf(facts.Attendees, operatorNames),
            RoundsOf(withRounds, startedAt, sponsorNames),
            drawLog,
            raffle.Authorities,
            AnnexOf(facts.Attendees))
        {
            Reinstatements = ReinstatementsOf(facts.Attendees, withRounds.Draws, operatorNames),
            LinkedRaffleNames = facts.LinkedRaffleNames,
        };
    }

    private static IReadOnlyList<NotarialRecordReinstatement> ReinstatementsOf(
        IReadOnlyList<RaffleAttendee> attendees,
        IReadOnlyList<PrizeDraw> draws,
        IReadOnlyDictionary<Guid, string?> operatorNames)
    {
        // R12: una sola pasada O(sorteos) para la última anulación por asistente.
        var latestVoidByAttendee = new Dictionary<Guid, DateTimeOffset>();
        foreach (var draw in draws)
        {
            if (!draw.IsLive && draw.VoidedAt is { } voidedAt)
            {
                if (!latestVoidByAttendee.TryGetValue(draw.WinnerAttendeeId, out var prev) || voidedAt > prev)
                    latestVoidByAttendee[draw.WinnerAttendeeId] = voidedAt;
            }
        }

        return attendees
            .Where(a => a.ReinstatedAt is { } reinstatedAt
                && a.ReinstatedById is not null
                && latestVoidByAttendee.TryGetValue(a.Id, out var lastVoid)
                && reinstatedAt >= lastVoid)
            .OrderBy(a => a.ReinstatedAt)
            .ThenBy(a => a.LastName, NameOrder)
            .ThenBy(a => a.FirstName, NameOrder)
            .Select(a => new NotarialRecordReinstatement(
                a.DisplayName,
                a.Company,
                a.ReinstatedById!.Value,
                operatorNames.GetValueOrDefault(a.ReinstatedById.Value),
                a.ReinstatedAt!.Value))
            .ToList();
    }

    private static NotarialRecordArithmetic ArithmeticOf(IReadOnlyList<RaffleAttendee> attendees)
    {
        var excluded = attendees.Where(a => !a.Qualified).ToList();
        var byReason = ExclusionReason.InEvaluationOrder
            .Select(reason => new NotarialRecordExclusion(reason, excluded.Count(a => a.ExclusionReason == reason)))
            .Where(r => r.Count > 0)
            .ToList();

        return new NotarialRecordArithmetic(attendees.Count, attendees.Count - excluded.Count, excluded.Count, byReason);
    }

    private static IReadOnlyList<NotarialRecordDecision> DecisionsOf(
        IReadOnlyList<RaffleAttendee> attendees,
        IReadOnlyDictionary<Guid, string?> operatorNames) => attendees
        .Where(a => a is { ManualDecision: not null, ManualDecidedById: not null, ManualDecidedAt: not null })
        .OrderBy(a => a.ManualDecidedAt)
        .ThenBy(a => a.LastName, NameOrder)
        .ThenBy(a => a.FirstName, NameOrder)
        .Select(a => new NotarialRecordDecision(
            a.DisplayName,
            a.Company,
            a.ManualDecision!,
            a.OverruledReason,
            TookEffect: a.ManualDecision == AttendeeOverrideDecision.Included ? a.Qualified : !a.Qualified,
            a.ManualDecidedById!.Value,
            operatorNames.GetValueOrDefault(a.ManualDecidedById.Value),
            a.ManualDecidedAt!.Value))
        .ToList();

    /// <summary>Las rondas por número y sus premios por creación, como ya los ordena el agregado.</summary>
    private static IReadOnlyList<NotarialRecordRound> RoundsOf(
        RaffleWithRounds withRounds,
        DateTimeOffset startedAt,
        IReadOnlyDictionary<Guid, string> sponsorNames)
    {
        // Una pasada por los sorteos y no una por premio: con 10.000 de cada lado eran 10⁸ (A31).
        var drawsByPrize = withRounds.Draws.ToLookup(d => d.PrizeId);

        return withRounds.Rounds
            .Select(round => new NotarialRecordRound(
                round.Number,
                round.Name,
                round.Prizes
                    .Select(prize =>
                    {
                        var live = drawsByPrize[prize.Id]
                            .Where(d => d.IsLive)
                            .OrderByDescending(d => d.DrawnAt)
                            .ThenByDescending(d => d.Id)
                            .FirstOrDefault();
                        return new NotarialRecordPrize(
                            prize.Name,
                            prize.Value,
                            live?.WinnerName,
                            live?.WinnerCompany,
                            AddedDuringRaffle: prize.CreatedAt > startedAt,
                            SponsorCompany: prize.SponsorCompanyId is { } sponsorId
                                ? sponsorNames.GetValueOrDefault(sponsorId)
                                : prize.SponsorName);
                    })
                    .ToList()))
            .ToList();
    }

    private static IReadOnlyList<NotarialRecordDrawLine> DrawLogOf(RaffleWithRounds withRounds)
    {
        var placeOf = withRounds.Rounds
            .SelectMany(round => round.Prizes.Select(prize => (prize.Id, Round: round, Prize: prize)))
            .ToDictionary(p => p.Id);

        return withRounds.Draws
            .OrderBy(d => d.DrawnAt)
            .ThenBy(d => d.Id)
            .Select(draw =>
            {
                var (_, round, prize) = placeOf[draw.PrizeId];
                return new NotarialRecordDrawLine(
                    draw.DrawnAt,
                    round.Number,
                    round.Name,
                    prize.Name,
                    draw.WinnerName,
                    draw.WinnerCompany,
                    draw.EligiblePoolSize,
                    draw.OnePrizePerPerson,
                    draw.OnePrizePerCompany,
                    draw.VoidCode,
                    draw.VoidedAt,
                    draw.VoidNote);
            })
            .ToList();
    }

    private static IReadOnlyList<NotarialRecordParticipant> AnnexOf(IReadOnlyList<RaffleAttendee> attendees) => attendees
        .Where(a => a.Qualified)
        .OrderBy(a => a.LastName, NameOrder)
        .ThenBy(a => a.FirstName, NameOrder)
        .ThenBy(a => a.Id)
        .Select((a, index) => new NotarialRecordParticipant(index + 1, a.DisplayName, a.Company))
        .ToList();
}

/// <summary>
/// La versión anterior que una reemisión reemplaza, con cuándo se generó: lo que la versión nueva dice
/// en su primera página (spec 03 §4.5). Solo la inmediatamente anterior.
/// </summary>
public sealed record NotarialRecordReplaces(int Version, DateTimeOffset GeneratedAt);

/// <summary>
/// La puerta en números (spec 03 §4.2 punto 7): registrados, en el bombo y fuera, con los de fuera
/// por motivo en el orden de spec 01 §6. Los motivos sin nadie no aparecen.
/// </summary>
public sealed record NotarialRecordArithmetic(
    int CheckedIn,
    int Qualified,
    int NotQualified,
    IReadOnlyList<NotarialRecordExclusion> ExcludedByReason);

/// <summary>Cuántos quedaron fuera por un motivo, con el código del motivo.</summary>
public sealed record NotarialRecordExclusion(string Reason, int Count);

/// <summary>
/// Una decisión manual, con nombre (spec 03 §4.2 punto 8). <see cref="DecidedByName"/> nulo es que el
/// realm no pudo decirlo; el acta escribe entonces usuario no disponible con el id. Sin la nota.
/// <para>
/// <see cref="TookEffect"/> es si la persona quedó donde la decisión quería: una inclusión que pierde
/// contra una regla que nadie vence (spec 01 §6) queda escrita, y el acta no puede decir que entró.
/// <see cref="OverruledReason"/> es la regla vencida, o la que la venció.
/// </para>
/// </summary>
public sealed record NotarialRecordDecision(
    string Name,
    string? Company,
    string Decision,
    string? OverruledReason,
    bool TookEffect,
    Guid DecidedById,
    string? DecidedByName,
    DateTimeOffset DecidedAt);

/// <summary>Una ronda como encabezado, con sus premios en orden de creación (spec 03 §4.2 punto 9).</summary>
public sealed record NotarialRecordRound(int Number, string Name, IReadOnlyList<NotarialRecordPrize> Prizes);

/// <summary>
/// Un premio y su resultado: el ganador vivo, o nadie si no se adjudicó. El valor va tal como se
/// escribió (spec 00 §1). El patrocinador es solo el nombre de su empresa, nunca el contacto (spec 03
/// §4.2 punto 9).
/// </summary>
public sealed record NotarialRecordPrize(
    string Name,
    string? Value,
    string? WinnerName,
    string? WinnerCompany,
    bool AddedDuringRaffle,
    string? SponsorCompany = null);

/// <summary>
/// Un sorteo de premio en el registro cronológico, vivo o anulado (spec 03 §4.2 punto 10). Una
/// anulación trae su código, su hora y su nota, que sí se imprime. Lleva las dos reglas bajo las que
/// se sorteó, que el acta imprime cuando no todos los sorteos salieron bajo las mismas.
/// </summary>
public sealed record NotarialRecordDrawLine(
    DateTimeOffset DrawnAt,
    int RoundNumber,
    string RoundName,
    string PrizeName,
    string WinnerName,
    string? WinnerCompany,
    int EligiblePoolSize,
    bool OnePrizePerPerson,
    bool OnePrizePerCompany,
    string? VoidCode,
    DateTimeOffset? VoidedAt,
    string? VoidNote);

/// <summary>Una persona del bombo en el anexo, numerada, con nombre y empresa y sin correo.</summary>
public sealed record NotarialRecordParticipant(int Number, string Name, string? Company);

/// <summary>Un participante anulado que fue reactivado para volver al bombo del sorteo.</summary>
public sealed record NotarialRecordReinstatement(
    string Name,
    string? Company,
    Guid ReinstatedById,
    string? ReinstatedByName,
    DateTimeOffset ReinstatedAt);

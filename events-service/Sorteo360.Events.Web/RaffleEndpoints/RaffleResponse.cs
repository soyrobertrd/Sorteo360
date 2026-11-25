using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

public record RaffleResponse
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string State { get; init; } = string.Empty;
    public string VisualStyle { get; init; } = string.Empty;
    public bool ShowPrizeOnScreen { get; init; }
    public int AnimationSeconds { get; init; }
    public bool RequiresPresenceConfirmation { get; init; }
    public bool RequiresCheckIn { get; init; } = true;
    public bool OnePrizePerPerson { get; init; }
    public bool OnePrizePerCompany { get; init; }
    public bool OnePrizePerDepartmentPerRound => OnePrizePerCompany;

    /// <summary>El evento al que está vinculado: sus sorteos no repiten ganadores. Nulo sin vínculo.</summary>
    public Guid? LinkedEventId { get; init; }
    public string? BoardTitle { get; init; }
    public string? BoardSubtitle { get; init; }
    public string? BoardIdentifier { get; init; }
    public int? BoardBatchSize { get; init; }
    public RaffleDisplayFieldsResponse DisplayFields { get; init; } = new(
        RaffleDisplayFields.Default.Name,
        RaffleDisplayFields.Default.Identifier,
        RaffleDisplayFields.Default.Detail);
    public string? NotaryName { get; init; }
    public string? NotaryIdentityDocument { get; init; }
    public string? WitnessOneName { get; init; }
    public string? WitnessOneIdentityDocument { get; init; }
    public string? WitnessTwoName { get; init; }
    public string? WitnessTwoIdentityDocument { get; init; }

    /// <summary>Cuándo se arrancó y se congeló la lista de la puerta. Nulo en <c>draft</c>.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Quién lo arrancó, como id. Nulo en <c>draft</c>.</summary>
    public Guid? StartedById { get; init; }

    /// <summary>
    /// La lista de DO Travel Awards que se aplicó al arrancar. Nulo en <c>draft</c> y cuando no había
    /// ninguna en vigor.
    /// </summary>
    public Guid? AwardsBatchId { get; init; }

    /// <summary>Dónde y hasta cuándo se retiran los premios. Se exige al completar.</summary>
    public string? PickupInstructions { get; init; }

    /// <summary>
    /// Los firmantes que el certificado de premio propone por omisión, en el orden de sus líneas. Vacía
    /// si no se guardó ninguno.
    /// </summary>
    public IReadOnlyList<CertificateSignerResponse> CertificateSigners { get; init; } = [];

    /// <summary>Cuándo se completó, el mismo instante que dice el acta. Nulo salvo en <c>completed</c>.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Quién lo completó, como id. Nulo salvo en <c>completed</c>.</summary>
    public Guid? CompletedById { get; init; }

    /// <summary>
    /// Las versiones del acta, de la más nueva a la más vieja (spec 03 §4.6). Vacía hasta completar.
    /// Los bytes se leen de <c>GET /v1/files/{storedFileId}/content</c>.
    /// </summary>
    public IReadOnlyList<NotarialRecordResponse> NotarialRecords { get; init; } = [];

    /// <summary>Las rondas del sorteo, en el orden en que corren, cada una con sus premios.</summary>
    public IReadOnlyList<RaffleRoundResponse> Rounds { get; init; } = [];

    /// <summary>
    /// Si el sorteo puede arrancar: verdadero exactamente cuando <see cref="BlockingReasons"/> está
    /// vacía. Nulo fuera de <c>draft</c>, donde no hay arranque para el que estar listo.
    /// </summary>
    public bool? IsReadyToStart { get; init; }

    /// <summary>
    /// Lo que impide arrancar: <c>no_qualified_attendees</c>, <c>attendance_unavailable</c> o
    /// <c>attendance_too_many_rows</c>. Nulo fuera de <c>draft</c>.
    /// </summary>
    public IReadOnlyList<string>? BlockingReasons { get; init; }

    /// <summary>
    /// Lo que conviene saber sin que frene: <c>awards_list_not_in_force</c>,
    /// <c>attendance_copy_stale</c> y <c>no_prizes</c>. Fuera de <c>draft</c> no trae
    /// <c>attendance_copy_stale</c>: después del congelado nada es una copia.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Asistentes con al menos una anulación que fueron reactivados para volver al bombo.
    /// </summary>
    public IReadOnlyList<Guid> ReinstatedAttendeeIds { get; init; } = [];

    /// <summary>
    /// El sorteo con los nombres de sus patrocinadores y operadores resueltos. Es la única forma
    /// de armarlo, así que todas las rutas que lo devuelven nombran igual (spec 00 §8).
    /// </summary>
    public static async Task<RaffleResponse> FromAsync(
        RaffleDetail detail,
        ICompanyDirectory companyDirectory,
        IContactNameDirectory contactNames,
        IRaffleOperatorDirectory? operators,
        CancellationToken cancellationToken)
    {
        var sponsorNames = await RaffleSponsorNames.ResolveAsync(
            detail.Raffle, companyDirectory, contactNames, cancellationToken);
        var operatorNames = await ResolveOperatorNamesAsync(detail.Raffle.Draws, operators, cancellationToken);
        return From(detail, sponsorNames, operatorNames);
    }

    public static Task<RaffleResponse> FromAsync(
        RaffleDetail detail,
        ICompanyDirectory companyDirectory,
        IContactNameDirectory contactNames,
        CancellationToken cancellationToken) =>
        FromAsync(detail, companyDirectory, contactNames, operators: null, cancellationToken);

    internal static async Task<IReadOnlyDictionary<Guid, string?>?> ResolveOperatorNamesAsync(
        IReadOnlyList<PrizeDraw> draws,
        IRaffleOperatorDirectory? operators,
        CancellationToken cancellationToken)
    {
        if (operators is null || draws.Count == 0)
            return null;

        var ids = new HashSet<Guid>();
        foreach (var draw in draws)
        {
            ids.Add(draw.DrawnById);
            if (draw.VoidedById.HasValue)
                ids.Add(draw.VoidedById.Value);
            foreach (var handover in draw.Handovers)
                ids.Add(handover.RedeemedById);
        }

        if (ids.Count == 0)
            return null;

        return await operators.ResolveNamesAsync(ids, cancellationToken);
    }

    private static RaffleResponse From(
        RaffleDetail detail,
        RaffleSponsorNames sponsorNames,
        IReadOnlyDictionary<Guid, string?>? operatorNames) => From(detail.Raffle.Raffle) with
    {
        Rounds = detail.Raffle.Rounds.Select(r => RaffleRoundResponse.From(r, detail.Raffle, sponsorNames, operatorNames)).ToList(),
        IsReadyToStart = detail.Readiness.IsReadyToStart,
        BlockingReasons = detail.Readiness.BlockingReasons,
        Warnings = detail.Readiness.Warnings,
        NotarialRecords = detail.Raffle.NotarialRecords.Select(NotarialRecordResponse.From).ToList(),
        ReinstatedAttendeeIds = detail.Raffle.ReinstatedAttendeeIds,
    };

    private static RaffleResponse From(Raffle raffle) => new()
    {
        Id = raffle.Id,
        EventId = raffle.EventId,
        State = raffle.State,
        VisualStyle = raffle.VisualStyle,
        ShowPrizeOnScreen = raffle.ShowPrizeOnScreen,
        AnimationSeconds = raffle.AnimationSeconds,
        RequiresPresenceConfirmation = raffle.RequiresPresenceConfirmation,
        RequiresCheckIn = raffle.RequiresCheckIn,
        OnePrizePerPerson = raffle.OnePrizePerPerson,
        OnePrizePerCompany = raffle.OnePrizePerCompany,
        LinkedEventId = raffle.LinkedEventId,
        BoardTitle = raffle.BoardTitle,
        BoardSubtitle = raffle.BoardSubtitle,
        BoardIdentifier = raffle.BoardIdentifier,
        BoardBatchSize = raffle.BoardBatchSize,
        NotaryName = raffle.Authorities.NotaryName,
        NotaryIdentityDocument = raffle.Authorities.NotaryIdentityDocument,
        WitnessOneName = raffle.Authorities.WitnessOneName,
        WitnessOneIdentityDocument = raffle.Authorities.WitnessOneIdentityDocument,
        WitnessTwoName = raffle.Authorities.WitnessTwoName,
        WitnessTwoIdentityDocument = raffle.Authorities.WitnessTwoIdentityDocument,
        StartedAt = raffle.StartedAt,
        StartedById = raffle.StartedById,
        AwardsBatchId = raffle.AwardsBatchId,
        PickupInstructions = raffle.PickupInstructions,
        CertificateSigners = raffle.CertificateSigners.InOrder
            .Select(s => new CertificateSignerResponse(s.Name, s.Title))
            .ToList(),
        CompletedAt = raffle.CompletedAt,
        CompletedById = raffle.CompletedById,
        DisplayFields = new RaffleDisplayFieldsResponse(
            raffle.DisplayFields.Name,
            raffle.DisplayFields.Identifier,
            raffle.DisplayFields.Detail),
    };
}

/// <summary>Una versión del acta notarial: su número de versión, el archivo y quién la generó.</summary>
public record NotarialRecordResponse
{
    public int Version { get; init; }
    public Guid StoredFileId { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public Guid GeneratedById { get; init; }

    public static NotarialRecordResponse From(NotarialRecord record) => new()
    {
        Version = record.Version,
        StoredFileId = record.StoredFileId,
        GeneratedAt = record.GeneratedAt,
        GeneratedById = record.GeneratedById,
    };
}

/// <summary>
/// Una ronda del sorteo, con sus premios en el orden en que se crearon, cada uno con su estado y sus
/// sorteos.
/// </summary>
public record RaffleRoundResponse
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<PrizeResponse> Prizes { get; init; } = [];

    public static RaffleRoundResponse From(
        RaffleRound round,
        RaffleWithRounds raffle,
        RaffleSponsorNames sponsorNames,
        IReadOnlyDictionary<Guid, string?>? operatorNames = null) => new()
    {
        Id = round.Id,
        Number = round.Number,
        Name = round.Name,
        Description = round.Description,
        Prizes = round.Prizes
            .Select(p => PrizeResponse.From(p, raffle.DrawsOf(p.Id), sponsorNames, raffle.SponsorLogoOf(p), operatorNames))
            .ToList(),
    };
}

/// <summary>Un firmante del certificado de premio: el nombre y el cargo impresos bajo su línea.</summary>
public sealed record CertificateSignerResponse(string Name, string Title);

/// <summary>Campos visibles del participante configurados para el sorteo (§10).</summary>
public sealed record RaffleDisplayFieldsResponse(
    string Name,
    string Identifier,
    string Detail);

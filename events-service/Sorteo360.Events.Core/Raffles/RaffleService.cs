using System.Globalization;
using System.Security.Cryptography;
using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.CrmDirectory;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Crea, lee y edita el sorteo de un evento, y arma sus rondas y premios. Las comprobaciones
/// corren en un orden fijo: que el evento exista, que el sorteo exista (o no, al crear), que el
/// evento admita escrituras y por último las reglas del propio sorteo, de modo que siempre gana el
/// error más específico.
/// <para>
/// El estado del evento frena solo la mitad de armado —crear y editar el sorteo, y todo lo de sus
/// rondas y premios— y nunca la lectura (spec 00 §5).
/// </para>
/// </summary>
public sealed class RaffleService : IRaffleService
{
    /// <summary>
    /// Cuántos asistentes admite un sorteo. Es una cota de cordura sobre una lectura de la puerta
    /// sin paginar, y no el tope de lote del CRM: la consulta de contactabilidad se parte en tandas
    /// de a mil (spec 01 §3.1). El techo es del sorteo y no de la puerta, así que es acá donde se
    /// cuenta y se rechaza (spec 01 §7).
    /// </summary>
    private const int MaxAttendees = 25_000;

    /// <summary>El tipo con que se guarda y se vuelve a servir el acta.</summary>
    private const string NotarialRecordContentType = "application/pdf";

    /// <summary>
    /// Cómo se ordenan los nombres de la puerta: en español y sin distinguir mayúsculas. Una
    /// comparación ordinal mandaría a "Álvarez" detrás de "Zapata", porque la Á está por encima de
    /// todo el ASCII, y la ñ tiene que ir después de la n.
    /// </summary>
    private static readonly StringComparer NameOrder =
        StringComparer.Create(CultureInfo.GetCultureInfo("es-DO"), ignoreCase: true);

    private readonly IRaffleRepository _repository;
    private readonly IEventRepository _eventRepository;
    private readonly IRecentAttendance _attendance;
    private readonly IAttendanceSource _liveAttendance;
    private readonly IAwardsMembership _awards;
    private readonly IContactMailability _mailability;
    private readonly CrmDirectoryStatusContext _crmStatus;
    private readonly IUploadPolicy _uploadPolicy;
    private readonly IFileStore _fileStore;
    private readonly INotarialRecordRenderer _recordRenderer;
    private readonly IRaffleOperatorDirectory _operators;
    private readonly IRaffleWinnerNotifier _winnerNotifier;
    private readonly ICompanyDirectory _companies;
    private readonly ICompanyContactDirectory _companyContacts;
    private readonly TimeProvider _clock;
    private readonly IPlanLimitGuard? _planLimits;

    public RaffleService(
        IRaffleRepository repository,
        IEventRepository eventRepository,
        IRecentAttendance attendance,
        IAttendanceSource liveAttendance,
        IAwardsMembership awards,
        IContactMailability mailability,
        CrmDirectoryStatusContext crmStatus,
        IUploadPolicy uploadPolicy,
        IFileStore fileStore,
        INotarialRecordRenderer recordRenderer,
        IRaffleOperatorDirectory operators,
        IRaffleWinnerNotifier winnerNotifier,
        ICompanyDirectory companies,
        ICompanyContactDirectory companyContacts,
        TimeProvider clock,
        IPlanLimitGuard? planLimits = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventRepository);
        ArgumentNullException.ThrowIfNull(attendance);
        ArgumentNullException.ThrowIfNull(liveAttendance);
        ArgumentNullException.ThrowIfNull(awards);
        ArgumentNullException.ThrowIfNull(mailability);
        ArgumentNullException.ThrowIfNull(crmStatus);
        ArgumentNullException.ThrowIfNull(uploadPolicy);
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(recordRenderer);
        ArgumentNullException.ThrowIfNull(operators);
        ArgumentNullException.ThrowIfNull(winnerNotifier);
        ArgumentNullException.ThrowIfNull(companies);
        ArgumentNullException.ThrowIfNull(companyContacts);
        ArgumentNullException.ThrowIfNull(clock);
        _repository = repository;
        _eventRepository = eventRepository;
        _attendance = attendance;
        _liveAttendance = liveAttendance;
        _awards = awards;
        _mailability = mailability;
        _crmStatus = crmStatus;
        _uploadPolicy = uploadPolicy;
        _fileStore = fileStore;
        _recordRenderer = recordRenderer;
        _operators = operators;
        _winnerNotifier = winnerNotifier;
        _companies = companies;
        _companyContacts = companyContacts;
        _clock = clock;
        _planLimits = planLimits;
    }

    public async Task<Result<Guid, BaseError>> CreateAsync(Guid eventId, Guid createdById, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<Guid, BaseError>(statusResult.Error);

        var loadResult = await _repository.GetByEventIdAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<Guid, BaseError>(loadResult.Error);
        if (loadResult.Value.HasValue)
            return Result.Failure<Guid, BaseError>(RaffleErrors.AlreadyExists());

        if (!EventStatus.Active.Contains(statusResult.Value))
            return Result.Failure<Guid, BaseError>(QuoteErrors.EventNoEditable());

        var raffle = Raffle.CreateDraft(eventId, createdById, _clock.GetUtcNow());
        return await _repository.AddAsync(raffle, cancellationToken);
    }

    public async Task<Result<RaffleDetail, BaseError>> GetAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(statusResult.Error);

        var loadResult = await LoadWithRoundsAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(loadResult.Error);

        var readiness = await GetReadinessAsync(loadResult.Value, cancellationToken);
        if (readiness.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(readiness.Error);

        return Result.Success<RaffleDetail, BaseError>(new RaffleDetail(loadResult.Value, readiness.Value));
    }

    /// <summary>
    /// Fuera del borrador es la tabla congelada y nada más: ni la puerta, ni DO Travel Awards, ni el
    /// CRM, así que tampoco hay estado del CRM que informar. La lista se leyó en vivo al arrancar,
    /// por eso es <c>ok</c> y de esa hora (spec 01 §8).
    /// </summary>
    public async Task<Result<RaffleAttendees, BaseError>> GetAttendeesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleAttendees, BaseError>(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleAttendees, BaseError>(loadResult.Error);

        var raffle = loadResult.Value;
        if (raffle.State != RaffleState.Draft)
        {
            var frozen = await _repository.ListFrozenAttendeesAsync(raffle.Id, cancellationToken);
            if (frozen.IsFailure)
                return Result.Failure<RaffleAttendees, BaseError>(frozen.Error);

            return Result.Success<RaffleAttendees, BaseError>(new RaffleAttendees(
                AttendanceStatus.Ok, raffle.StartedAt, raffle.AwardsBatchId, InDisplayOrder(frozen.Value)));
        }

        var drumResult = await ComputeDrumAsync(raffle, cancellationToken);
        if (drumResult.IsFailure)
            return Result.Failure<RaffleAttendees, BaseError>(drumResult.Error);

        var drum = drumResult.Value;
        if (drum.TooManyRows)
            return Result.Failure<RaffleAttendees, BaseError>(RaffleErrors.AttendanceTooManyRows());

        return Result.Success<RaffleAttendees, BaseError>(new RaffleAttendees(
            drum.Attendance.Status,
            drum.Attendance.FetchedAt,
            drum.AwardsList.HasValue ? drum.AwardsList.Value.BatchId : null,
            drum.Qualification is null
                ? null
                : InDisplayOrder(drum.Qualification.Attendees.Select(RaffleAttendee.From))));
    }

    /// <summary>
    /// Las comprobaciones en el orden de spec 01 §9: las baratas primero, después la puerta y el CRM
    /// en vivo —antes del bloqueo, para que una lectura lenta nunca lo retenga— y por último, bajo el
    /// bloqueo, el estado otra vez, las decisiones manuales, el pipeline y la escritura.
    /// <para>
    /// Al CRM se le pregunta por todos los contactos de la puerta y no solo por los de las decisiones
    /// manuales, así que leer esas decisiones después no cambia a quién se le pregunta. Con el CRM
    /// caído el arranque falla, a diferencia de la lectura del borrador: un bombo congelado sin la
    /// lista negra no tiene arreglo.
    /// </para>
    /// </summary>
    public async Task<Result<RaffleDetail, BaseError>> StartAsync(Guid eventId, Guid startedById, CancellationToken cancellationToken)
    {
        var loadResult = await LoadDraftForWriteAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(loadResult.Error);

        var door = loadResult.Value.RequiresCheckIn
            ? await _liveAttendance.GetCheckedInAsync(eventId, cancellationToken)
            : await _liveAttendance.GetAttendeesAsync(eventId, requiresCheckIn: false, cancellationToken);
        if (door.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(door.Error);

        if (door.Value.Count > MaxAttendees)
            return Result.Failure<RaffleDetail, BaseError>(RaffleErrors.AttendanceTooManyRows());

        var awardsList = await _awards.GetInForceAsync(cancellationToken);

        var verdicts = await AskMailabilityLiveAsync(door.Value, cancellationToken);
        if (verdicts.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(verdicts.Error);

        var now = _clock.GetUtcNow();
        var writeResult = await _repository.StartAsync(
            loadResult.Value.Id,
            (locked, overrides) => RaffleFreeze.Create(
                locked, door.Value, awardsList, overrides, verdicts.Value, startedById, now),
            cancellationToken);
        if (writeResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(writeResult.Error);

        return await GetAsync(eventId, cancellationToken);
    }

    /// <summary>
    /// En el orden de spec 03 §4.4: que existan el evento y el sorteo, y todo lo demás bajo el bloqueo
    /// del sorteo —su estado, las autoridades, las instrucciones de retiro, los nombres de los
    /// operadores, el PDF y su guardado—. El estado del evento no se mira: un sorteo que arrancó tiene
    /// que poder terminar (spec 00 §5).
    /// <para>
    /// Falla cerrado: con el almacenamiento caído es <c>FILE_STORE_UNAVAILABLE</c>, no se escribe nada
    /// y el sorteo sigue en curso. Si algo falla después de guardar, los bytes se descartan.
    /// </para>
    /// <para>
    /// Después de confirmar le escribe a cada ganador vivo (spec 03 §5.1), y la respuesta se arma
    /// después de los envíos, así que ya dice a quién se le avisó.
    /// </para>
    /// </summary>
    public async Task<Result<RaffleDetail, BaseError>> CompleteAsync(Guid eventId, Guid completedById, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(loadResult.Error);

        // Un solo instante para la fila, el archivo y lo que dice el acta (spec 03 §4.4 paso 3).
        var now = _clock.GetUtcNow();
        var writeResult = await WriteWithNotarialRecordAsync(
            completedById,
            now,
            produce => _repository.CompleteAsync(loadResult.Value.Id, completedById, now, produce, cancellationToken),
            cancellationToken);
        if (writeResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(writeResult.Error);

        return await GetAsync(eventId, cancellationToken);
    }

    /// <summary>
    /// Un correo por sorteo de premio vivo, en el orden de los premios, y su desenlace guardado en el
    /// sorteo apenas vuelve el POST: si el proceso se cae a mitad, los <c>sent</c> ya alcanzados
    /// quedan y el resto sigue nulo (spec 03 §5.1). Un sorteo anulado no se avisa.
    /// <para>
    /// Si no se puede releer el sorteo o el evento no se manda nada.
    /// </para>
    /// <para>
    /// Las dos relecturas no se cancelan: el sorteo ya está completado, y una cancelación que saliera
    /// de ellas dejaría a todos los ganadores sin correo.
    /// </para>
    /// </summary>
    public async Task NotifyWinnersAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var raffleResult = await _repository.GetWithRoundsByEventIdAsync(eventId, CancellationToken.None);
        if (raffleResult.IsFailure || raffleResult.Value.HasNoValue)
            return;

        var eventResult = await _eventRepository.GetByIdAsync(eventId, CancellationToken.None);
        if (eventResult.IsFailure || eventResult.Value.HasNoValue)
            return;

        var raffle = raffleResult.Value.Value;
        var @event = eventResult.Value.Value.Event;

        // Una pasada por los sorteos y no una por premio: con 10.000 de cada lado eran 10⁸ (A31).
        var liveByPrize = raffle.Draws
            .Where(d => d.IsLive)
            .ToLookup(d => d.PrizeId);

        foreach (var prize in raffle.Rounds.SelectMany(r => r.Prizes))
        {
            var draw = liveByPrize[prize.Id].OrderByDescending(d => d.DrawnAt).ThenByDescending(d => d.Id).FirstOrDefault();
            if (draw is null)
                continue;

            var outcome = await _winnerNotifier.NotifyWonAsync(@event, raffle.Raffle, prize, draw, cancellationToken);

            // Sin cancelar, a propósito y por lo mismo que en QuoteService.NotifyOutcomeAsync: el
            // sorteo ya está completado, y una cancelación que saliera por acá lo volvería un 500 y
            // dejaría el sorteo de premio sin decir qué pasó con su correo.
            await _repository.UpdateWinnerNotificationAsync(
                draw.Id, outcome.Status, outcome.NotificationId, CancellationToken.None);
        }
    }

    /// <summary>
    /// En el orden de spec 03 §4.5: que existan el evento y el sorteo, y todo lo demás bajo el bloqueo
    /// del sorteo, como al completar: su estado, la versión vigente, las autoridades nuevas, el PDF y su
    /// guardado. El estado del evento no se mira: el notario puede ver el error con el evento ya
    /// cerrado (spec 00 §5). No envía ningún correo; el del ganador va con el sorteo, no con el acta.
    /// <para>
    /// Falla cerrado como completar: con el almacenamiento caído es <c>FILE_STORE_UNAVAILABLE</c>, no
    /// se escribe nada y la versión vigente sigue siéndolo.
    /// </para>
    /// </summary>
    public async Task<Result<RaffleDetail, BaseError>> ReissueAsync(
        Guid eventId, RaffleAuthorities authorities, Guid reissuedById, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorities);

        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(loadResult.Error);

        var now = _clock.GetUtcNow();
        var writeResult = await WriteWithNotarialRecordAsync(
            reissuedById,
            now,
            produce => _repository.ReissueAsync(
                loadResult.Value.Id, authorities, reissuedById, now, produce, cancellationToken),
            cancellationToken);
        if (writeResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(writeResult.Error);

        return await GetAsync(eventId, cancellationToken);
    }

    /// <summary>
    /// Corre una escritura del repositorio que genera una versión del acta, con el único camino que la
    /// produce —nombres de los operadores, contenido, PDF, guardado— para que completar y reemitir
    /// rendericen igual (spec 03 §4.5). Si la escritura no confirma, o lanza, los bytes que se llegaron
    /// a guardar se descartan.
    /// </summary>
    private async Task<UnitResult<BaseError>> WriteWithNotarialRecordAsync(
        Guid generatedById,
        DateTimeOffset now,
        Func<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>, Task<UnitResult<BaseError>>> write,
        CancellationToken cancellationToken)
    {
        // Lo guardado, si se llegó a guardar: es lo que hay que descartar si la escritura no confirma.
        StoredFileLocation? saved = null;

        async Task<Result<StoredFile, BaseError>> ProduceAsync(
            NotarialRecordFacts facts, int version, NotarialRecordReplaces? replaces)
        {
            var names = await _operators.ResolveNamesAsync(facts.OperatorIds(), cancellationToken);
            var sponsorNames = await ResolveSponsorNamesAsync(facts.Raffle, cancellationToken);
            if (sponsorNames.IsFailure)
                return Result.Failure<StoredFile, BaseError>(sponsorNames.Error);

            var content = NotarialRecordContent.Build(facts, names, version, replaces, sponsorNames.Value);
            var bytes = _recordRenderer.Render(content);
            var fileName = $"acta-{content.RecordNumber}-v{content.Version}.pdf";

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                saved = await _fileStore.SaveAsync(stream, fileName, NotarialRecordContentType, cancellationToken);
            }
            catch (FileStoreUnavailableException)
            {
                return Result.Failure<StoredFile, BaseError>(FileErrors.FileStoreUnavailable());
            }

            return new StoredFile(
                id: Guid.NewGuid(),
                provider: saved.Provider,
                storageKey: saved.StorageKey,
                fileName: fileName,
                contentType: NotarialRecordContentType,
                sizeBytes: saved.SizeBytes,
                checksum: saved.Checksum,
                missingSince: null,
                createdAt: now,
                createdById: generatedById);
        }

        UnitResult<BaseError> writeResult;
        try
        {
            writeResult = await write(ProduceAsync);
        }
        catch when (saved is not null)
        {
            // Una cancelación a mitad de la escritura no pasa por el resultado, y los bytes tampoco
            // pueden quedar sin dueño.
            await _fileStore.DiscardOrphanBytesAsync(saved.StorageKey);
            throw;
        }

        if (writeResult.IsFailure && saved is not null)
            await _fileStore.DiscardOrphanBytesAsync(saved.StorageKey);

        return writeResult;
    }

    /// <summary>
    /// Los nombres de las empresas patrocinadoras que imprime el acta, desde la copia del directorio:
    /// un nombre viejo sirve. Si alguno no se puede nombrar de ninguna forma —sin copia y con el CRM
    /// caído— el acta no se genera, porque un documento notarial no imprime un patrocinador en blanco,
    /// y quien opera reintenta (spec 03 §4.2 punto 9).
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<Guid, string>, BaseError>> ResolveSponsorNamesAsync(
        RaffleWithRounds raffle, CancellationToken cancellationToken)
    {
        var sponsorIds = raffle.Rounds
            .SelectMany(r => r.Prizes)
            .Select(p => p.SponsorCompanyId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        if (sponsorIds.Count == 0)
            return Result.Success<IReadOnlyDictionary<Guid, string>, BaseError>(new Dictionary<Guid, string>());

        var lookup = await _companies.ResolveAsync(sponsorIds, cancellationToken);
        if (sponsorIds.Any(id => lookup.Snapshot(id) is null))
            return Result.Failure<IReadOnlyDictionary<Guid, string>, BaseError>(CompanyErrors.CrmUnavailable());

        return Result.Success<IReadOnlyDictionary<Guid, string>, BaseError>(
            sponsorIds.ToDictionary(id => id, id => lookup.Snapshot(id)!.Name));
    }

    /// <summary>
    /// En el orden de spec 02 §6: que existan el evento y el sorteo, que el evento admita escrituras
    /// —un sorteo reabierto en un evento cerrado ya no podría arrancar— y, bajo el bloqueo, el estado
    /// del sorteo y que no tenga ningún sorteo de premio, vivo o anulado.
    /// </summary>
    public async Task<Result<RaffleDetail, BaseError>> ReopenAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(loadResult.Error);

        if (!EventStatus.Active.Contains(statusResult.Value))
            return Result.Failure<RaffleDetail, BaseError>(QuoteErrors.EventNoEditable());

        var writeResult = await _repository.ReopenAsync(
            loadResult.Value.Id,
            (locked, draws) => locked.Reopen().Ensure(_ => draws.Count == 0, RaffleErrors.HasDraws()),
            cancellationToken);
        if (writeResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(writeResult.Error);

        return await GetAsync(eventId, cancellationToken);
    }

    /// <summary>
    /// En el orden de siempre: que existan el evento y el sorteo, que el evento admita escrituras y que
    /// el sorteo siga en draft; el repositorio lo vuelve a mirar bajo el bloqueo, por si arrancó entre
    /// tanto.
    /// </summary>
    public async Task<UnitResult<BaseError>> DeleteAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var loadResult = await LoadDraftForWriteAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        return await _repository.DeleteDraftAsync(loadResult.Value.Id, cancellationToken);
    }

    public Task<Result<PagedResult<RaffleListItem>, BaseError>> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        _repository.ListAsync(page, pageSize, cancellationToken);

    public Task<Result<RaffleTotals, BaseError>> GetTotalsAsync(CancellationToken cancellationToken) =>
        _repository.GetTotalsAsync(cancellationToken);

    public async Task<UnitResult<BaseError>> UpdateAsync(UpdateRaffleDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var statusResult = await GetEventStatusAsync(dto.EventId, cancellationToken);
        if (statusResult.IsFailure)
            return UnitResult.Failure(statusResult.Error);

        var loadResult = await LoadAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        if (!EventStatus.Active.Contains(statusResult.Value))
            return UnitResult.Failure(QuoteErrors.EventNoEditable());

        var updateResult = loadResult.Value.Update(dto, _clock.GetUtcNow());
        if (updateResult.IsFailure)
            return UnitResult.Failure(updateResult.Error);

        return await _repository.UpdateAsync(updateResult.Value, cancellationToken);
    }

    public async Task<UnitResult<BaseError>> SetCertificateSignersAsync(
        Guid eventId, CertificateSigners signers, Guid changedById, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signers);

        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return UnitResult.Failure(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        if (!EventStatus.Active.Contains(statusResult.Value))
            return UnitResult.Failure(QuoteErrors.EventNoEditable());

        var changed = loadResult.Value.ChangeCertificateSigners(signers, changedById, _clock.GetUtcNow());

        return await _repository.UpdateAsync(changed, cancellationToken);
    }

    public async Task<Result<RaffleDetail, BaseError>> SetDisplayFieldsAsync(
        Guid eventId, RaffleDisplayFields displayFields, Guid changedById, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(displayFields);

        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(loadResult.Error);

        if (!EventStatus.Active.Contains(statusResult.Value))
            return Result.Failure<RaffleDetail, BaseError>(QuoteErrors.EventNoEditable());

        var changed = loadResult.Value.ChangeDisplayFields(displayFields, changedById, _clock.GetUtcNow());
        if (changed.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(changed.Error);

        var saved = await _repository.UpdateAsync(changed.Value, cancellationToken);
        if (saved.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(saved.Error);

        return await GetAsync(eventId, cancellationToken);
    }

    public async Task<Result<Guid, BaseError>> CreateRoundAsync(CreateRaffleRoundDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadForSetupAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<Guid, BaseError>(loadResult.Error);

        var raffle = loadResult.Value;
        if (_planLimits is not null)
        {
            var limitCheck = await _planLimits.EnsureCanAddRoundAsync(dto.EventId, raffle.Rounds.Count, cancellationToken);
            if (limitCheck.IsFailure)
                return Result.Failure<Guid, BaseError>(limitCheck.Error);
        }

        var now = _clock.GetUtcNow();
        var round = new RaffleRound(
            id: Guid.NewGuid(),
            raffleId: raffle.Raffle.Id,
            number: raffle.NextRoundNumber,
            name: dto.Name,
            description: dto.Description,
            prizes: [],
            createdAt: now,
            updatedAt: now,
            createdById: dto.CreatedById,
            updatedById: dto.CreatedById);

        return await _repository.AddRoundAsync(round, cancellationToken);
    }

    public async Task<UnitResult<BaseError>> UpdateRoundAsync(UpdateRaffleRoundDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadForSetupAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        var roundResult = loadResult.Value.FindRound(dto.RoundId);
        if (roundResult.IsFailure)
            return UnitResult.Failure(roundResult.Error);

        var updated = roundResult.Value with
        {
            Name = dto.Name,
            Description = dto.Description,
            UpdatedAt = _clock.GetUtcNow(),
            UpdatedById = dto.UpdatedById,
        };

        var writeResult = await _repository.UpdateRoundAsync(updated, SetupGuards.EnsureRoundHasNoDraws, cancellationToken);
        return FoundOr(writeResult, RaffleErrors.RoundNotFound);
    }

    public async Task<UnitResult<BaseError>> DeleteRoundAsync(Guid eventId, Guid roundId, CancellationToken cancellationToken)
    {
        var loadResult = await LoadForSetupAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        var raffle = loadResult.Value;
        var canDelete = raffle.EnsureCanDeleteRound(roundId);
        if (canDelete.IsFailure)
            return canDelete;

        var writeResult = await _repository.DeleteRoundAsync(raffle.Raffle.Id, roundId, cancellationToken);
        return FoundOr(writeResult, RaffleErrors.RoundNotFound);
    }

    /// <summary>
    /// Los premios nacen idénticos salvo el identificador, y comparten el instante de creación: son
    /// un solo alta, y el orden entre ellos no significa nada.
    /// <para>
    /// El instante se trunca a microsegundos, la precisión de PostgreSQL: la respuesta devuelve estos
    /// mismos premios, y su <c>createdAt</c> tiene que coincidir con el que después lee el GET.
    /// </para>
    /// </summary>
    public async Task<Result<IReadOnlyList<Prize>, BaseError>> CreatePrizesAsync(CreatePrizesDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadForSetupAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<IReadOnlyList<Prize>, BaseError>(loadResult.Error);

        var roundResult = loadResult.Value.FindRound(dto.RoundId);
        if (roundResult.IsFailure)
            return Result.Failure<IReadOnlyList<Prize>, BaseError>(roundResult.Error);

        if (_planLimits is not null)
        {
            var currentPrizes = loadResult.Value.Rounds.Sum(r => r.Prizes.Count);
            var limitCheck = await _planLimits.EnsureCanAddPrizesAsync(dto.EventId, currentPrizes, dto.Quantity, cancellationToken);
            if (limitCheck.IsFailure)
                return Result.Failure<IReadOnlyList<Prize>, BaseError>(limitCheck.Error);
        }

        var sponsorCheck = await ValidateSponsorAsync(dto.SponsorCompanyId, dto.SponsorContactId, cancellationToken);
        if (sponsorCheck.IsFailure)
            return Result.Failure<IReadOnlyList<Prize>, BaseError>(sponsorCheck.Error);

        var utcNow = _clock.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        IReadOnlyList<Prize> prizes = Enumerable.Range(0, dto.Quantity)
            .Select(_ => new Prize(
                id: Guid.NewGuid(),
                roundId: dto.RoundId,
                name: dto.Name,
                description: dto.Description,
                value: dto.Value,
                createdAt: now,
                updatedAt: now,
                createdById: dto.CreatedById,
                updatedById: dto.CreatedById,
                sponsorCompanyId: dto.SponsorCompanyId,
                sponsorContactId: dto.SponsorContactId,
                sponsorName: dto.SponsorName))
            .ToList();

        var writeResult = await _repository.AddPrizesAsync(loadResult.Value.Raffle.Id, prizes, cancellationToken);
        return writeResult.IsFailure
            ? Result.Failure<IReadOnlyList<Prize>, BaseError>(writeResult.Error)
            : Result.Success<IReadOnlyList<Prize>, BaseError>(prizes);
    }

    public async Task<Result<ImportPrizesResult, BaseError>> ImportPrizesAsync(
        ImportPrizesDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var statusResult = await GetEventStatusAsync(dto.EventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<ImportPrizesResult, BaseError>(statusResult.Error);

        var loadResult = await LoadWithRoundsAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<ImportPrizesResult, BaseError>(loadResult.Error);

        if (!EventStatus.Active.Contains(statusResult.Value))
            return Result.Failure<ImportPrizesResult, BaseError>(QuoteErrors.EventNoEditable());

        var raffleWithRounds = loadResult.Value;
        var draftCheck = raffleWithRounds.Raffle.EnsureDraft();
        if (draftCheck.IsFailure)
            return Result.Failure<ImportPrizesResult, BaseError>(draftCheck.Error);

        var utcNow = _clock.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));

        var existingRounds = raffleWithRounds.Rounds;
        var maxExisting = existingRounds.Count > 0 ? existingRounds.Max(r => r.Number) : 0;
        var maxRequested = dto.Prizes.Max(p => p.RoundNumber);

        var newRounds = new List<RaffleRound>();
        for (var n = maxExisting + 1; n <= maxRequested; n++)
        {
            newRounds.Add(new RaffleRound(
                id: Guid.NewGuid(),
                raffleId: raffleWithRounds.Raffle.Id,
                number: n,
                name: $"Ronda {n}",
                description: null,
                prizes: [],
                createdAt: now,
                updatedAt: now,
                createdById: dto.CreatedById,
                updatedById: dto.CreatedById));
        }

        var totalAdding = dto.Prizes.Sum(p => p.Quantity);

        if (_planLimits is not null)
        {
            if (newRounds.Count > 0)
            {
                var roundLimit = await _planLimits.EnsureCanAddRoundAsync(
                    dto.EventId, existingRounds.Count, cancellationToken, newRounds.Count);
                if (roundLimit.IsFailure)
                    return Result.Failure<ImportPrizesResult, BaseError>(roundLimit.Error);
            }

            var currentPrizes = existingRounds.Sum(r => r.Prizes.Count);
            var prizeLimit = await _planLimits.EnsureCanAddPrizesAsync(
                dto.EventId, currentPrizes, totalAdding, cancellationToken);
            if (prizeLimit.IsFailure)
                return Result.Failure<ImportPrizesResult, BaseError>(prizeLimit.Error);
        }

        var roundByNumber = new Dictionary<int, Guid>(existingRounds.Count + newRounds.Count);
        foreach (var r in existingRounds)
            roundByNumber[r.Number] = r.Id;
        foreach (var r in newRounds)
            roundByNumber[r.Number] = r.Id;

        var firstRoundId = existingRounds
            .Concat(newRounds)
            .OrderBy(r => r.Number)
            .First()
            .Id;

        var newPrizes = new List<Prize>(totalAdding);
        foreach (var item in dto.Prizes)
        {
            var targetRoundId = roundByNumber.TryGetValue(item.RoundNumber, out var matched)
                ? matched
                : firstRoundId;
            var name = item.Name.Trim();
            var value = string.IsNullOrWhiteSpace(item.Value) ? null : item.Value.Trim();
            var sponsorName = string.IsNullOrWhiteSpace(item.SponsorName) ? null : item.SponsorName.Trim();

            for (var i = 0; i < item.Quantity; i++)
            {
                newPrizes.Add(new Prize(
                    id: Guid.NewGuid(),
                    roundId: targetRoundId,
                    name: name,
                    description: null,
                    value: value,
                    createdAt: now,
                    updatedAt: now,
                    createdById: dto.CreatedById,
                    updatedById: dto.CreatedById,
                    sponsorName: sponsorName));
            }
        }

        var writeResult = await _repository.ImportPrizesAsync(
            raffleWithRounds.Raffle.Id, newRounds, newPrizes, cancellationToken);
        if (writeResult.IsFailure)
            return Result.Failure<ImportPrizesResult, BaseError>(writeResult.Error);

        return Result.Success<ImportPrizesResult, BaseError>(
            new ImportPrizesResult(newPrizes.Count, newRounds.Count));
    }

    public async Task<UnitResult<BaseError>> UpdatePrizeAsync(UpdatePrizeDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadForSetupAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        var prizeResult = loadResult.Value.EnsureCanMovePrize(dto.PrizeId, dto.RoundId);
        if (prizeResult.IsFailure)
            return UnitResult.Failure(prizeResult.Error);

        var sponsorCheck = await ValidateSponsorAsync(dto.SponsorCompanyId, dto.SponsorContactId, cancellationToken);
        if (sponsorCheck.IsFailure)
            return sponsorCheck;

        var updated = prizeResult.Value with
        {
            RoundId = dto.RoundId,
            Name = dto.Name,
            Description = dto.Description,
            Value = dto.Value,
            SponsorCompanyId = dto.SponsorCompanyId,
            SponsorContactId = dto.SponsorContactId,
            SponsorName = string.IsNullOrWhiteSpace(dto.SponsorName) ? null : dto.SponsorName.Trim(),
            UpdatedAt = _clock.GetUtcNow(),
            UpdatedById = dto.UpdatedById,
        };

        var writeResult = await _repository.UpdatePrizeAsync(
            loadResult.Value.Raffle.Id,
            updated,
            draws => SetupGuards.EnsurePrizeHasNoDraws(updated.Id, draws),
            cancellationToken);
        return FoundOr(writeResult, RaffleErrors.PrizeNotFound);
    }

    /// <summary>
    /// El patrocinador se comprueba en vivo contra el CRM y falla cerrado, como la empresa de un
    /// proveedor (spec 00 §8): que la empresa haya existido alguna vez —una cerrada pasa—, y que el
    /// contacto, si viene, tenga hoy una vinculación vigente con ella. Corre después de las reglas del
    /// sorteo, para que un premio que ya no se puede editar no le pregunte nada al CRM.
    /// </summary>
    private async Task<UnitResult<BaseError>> ValidateSponsorAsync(
        Guid? companyId, Guid? contactId, CancellationToken cancellationToken)
    {
        if (companyId is not { } sponsorCompanyId)
            return UnitResult.Success<BaseError>();

        var company = await _companies.GetCompanyAsync(sponsorCompanyId, cancellationToken);
        if (company.IsFailure)
            return UnitResult.Failure(company.Error);
        if (company.Value.HasNoValue)
            return UnitResult.Failure(CompanyErrors.CompanyNotAvailable());

        if (contactId is not { } sponsorContactId)
            return UnitResult.Success<BaseError>();

        var contacts = await _companyContacts.GetContactsAsync(sponsorCompanyId, cancellationToken);
        if (contacts.HasNoValue)
            return UnitResult.Failure(CompanyErrors.CrmUnavailable());

        return contacts.Value.Any(c => c.Id == sponsorContactId)
            ? UnitResult.Success<BaseError>()
            : UnitResult.Failure(CompanyErrors.ContactNotAvailable());
    }

    public async Task<UnitResult<BaseError>> DeletePrizeAsync(Guid eventId, Guid prizeId, CancellationToken cancellationToken)
    {
        var loadResult = await LoadForSetupAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        var prizeResult = loadResult.Value.FindPrize(prizeId);
        if (prizeResult.IsFailure)
            return UnitResult.Failure(prizeResult.Error);

        var writeResult = await _repository.DeletePrizeAsync(
            loadResult.Value.Raffle.Id,
            prizeId,
            draws => SetupGuards.EnsurePrizeHasNoDraws(prizeId, draws),
            cancellationToken);
        return FoundOr(writeResult, RaffleErrors.PrizeNotFound);
    }

    /// <summary>
    /// En el orden de spec 00 §8, todo antes de guardar un byte: que existan el evento, el sorteo y el
    /// premio y admitan cambios; que el premio no tenga sorteos y tenga patrocinador; y que el archivo
    /// sea una imagen, según sus bytes. Lo del premio se vuelve a decidir bajo el bloqueo del sorteo, y
    /// un logo cuyo enlace no se escribió descarta sus bytes (ADR-0014).
    /// </summary>
    public async Task<Result<StoredFile, BaseError>> SetPrizeSponsorLogoAsync(
        SetPrizeSponsorLogoDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadForSetupAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<StoredFile, BaseError>(loadResult.Error);

        var raffle = loadResult.Value;
        var prizeResult = raffle.FindPrize(dto.PrizeId);
        if (prizeResult.IsFailure)
            return Result.Failure<StoredFile, BaseError>(prizeResult.Error);

        var canSet = SetupGuards.EnsureCanSetSponsorLogo(prizeResult.Value, raffle.Draws);
        if (canSet.IsFailure)
            return Result.Failure<StoredFile, BaseError>(canSet.Error);

        var inspected = await _uploadPolicy.InspectImageAsync(dto.Content, dto.FileName, dto.ContentType, cancellationToken);
        if (inspected.IsFailure)
            return Result.Failure<StoredFile, BaseError>(inspected.Error);

        // Lo que se guarda y se vuelve a servir es el tipo que dicen los bytes.
        var contentType = inspected.Value.ContentType;

        StoredFileLocation location;
        try
        {
            location = await _fileStore.SaveAsync(dto.Content, dto.FileName, contentType, cancellationToken);
        }
        catch (FileStoreUnavailableException)
        {
            return Result.Failure<StoredFile, BaseError>(FileErrors.FileStoreUnavailable());
        }

        var logo = new StoredFile(
            id: Guid.NewGuid(),
            provider: location.Provider,
            storageKey: location.StorageKey,
            fileName: dto.FileName,
            contentType: contentType,
            sizeBytes: location.SizeBytes,
            checksum: location.Checksum,
            missingSince: null,
            createdAt: _clock.GetUtcNow(),
            createdById: dto.UploadedById);

        var linked = await _repository.SetPrizeSponsorLogoAsync(
            raffle.Raffle.Id,
            dto.PrizeId,
            logo,
            SetupGuards.EnsureCanSetSponsorLogo,
            cancellationToken);
        if (linked.IsFailure)
        {
            await _fileStore.DiscardOrphanBytesAsync(location.StorageKey);
            return Result.Failure<StoredFile, BaseError>(linked.Error);
        }

        return Result.Success<StoredFile, BaseError>(logo);
    }

    public async Task<UnitResult<BaseError>> RemovePrizeSponsorLogoAsync(
        Guid eventId,
        Guid prizeId,
        Guid removedById,
        CancellationToken cancellationToken)
    {
        var loadResult = await LoadForSetupAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        var prizeResult = loadResult.Value.FindPrize(prizeId);
        if (prizeResult.IsFailure)
            return UnitResult.Failure(prizeResult.Error);

        // Solo se vacía el enlace. El archivo y sus bytes se quedan para la purga de retención.
        var writeResult = await _repository.RemovePrizeSponsorLogoAsync(
            loadResult.Value.Raffle.Id,
            prizeId,
            removedById,
            _clock.GetUtcNow(),
            draws => SetupGuards.EnsurePrizeHasNoDraws(prizeId, draws),
            cancellationToken);
        return FoundOr(writeResult, RaffleErrors.PrizeNotFound);
    }

    public async Task<Result<StoredFile, BaseError>> SetPrizesImageAsync(
        Guid eventId,
        IReadOnlyList<Guid> prizeIds,
        Stream content,
        string fileName,
        string? contentType,
        Guid uploadedById,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prizeIds);
        ArgumentNullException.ThrowIfNull(content);

        var raffleResult = await LoadRaffleOfEventAsync(eventId, cancellationToken);
        if (raffleResult.IsFailure)
            return Result.Failure<StoredFile, BaseError>(raffleResult.Error);

        var raffle = raffleResult.Value;
        if (raffle.State == RaffleState.Completed)
            return Result.Failure<StoredFile, BaseError>(RaffleErrors.Completed());

        var inspected = await _uploadPolicy.InspectImageAsync(content, fileName, contentType, cancellationToken);
        if (inspected.IsFailure)
            return Result.Failure<StoredFile, BaseError>(inspected.Error);

        var resolvedContentType = inspected.Value.ContentType;
        StoredFileLocation location;
        try
        {
            location = await _fileStore.SaveAsync(content, fileName, resolvedContentType, cancellationToken);
        }
        catch (FileStoreUnavailableException)
        {
            return Result.Failure<StoredFile, BaseError>(FileErrors.FileStoreUnavailable());
        }

        var now = _clock.GetUtcNow();
        var storedFile = new StoredFile(
            id: Guid.NewGuid(),
            provider: location.Provider,
            storageKey: location.StorageKey,
            fileName: fileName,
            contentType: resolvedContentType,
            sizeBytes: location.SizeBytes,
            checksum: location.Checksum,
            missingSince: null,
            createdAt: now,
            createdById: uploadedById);

        var linked = await _repository.SetPrizesImageAsync(
            raffle.Id,
            prizeIds,
            storedFile,
            uploadedById,
            now,
            cancellationToken);
        if (linked.IsFailure)
        {
            await _fileStore.DiscardOrphanBytesAsync(location.StorageKey);
            return Result.Failure<StoredFile, BaseError>(linked.Error);
        }

        if (!linked.Value)
        {
            await _fileStore.DiscardOrphanBytesAsync(location.StorageKey);
            return Result.Failure<StoredFile, BaseError>(RaffleErrors.PrizeNotFound());
        }

        return Result.Success<StoredFile, BaseError>(storedFile);
    }

    public async Task<UnitResult<BaseError>> RemovePrizesImageAsync(
        Guid eventId,
        IReadOnlyList<Guid> prizeIds,
        Guid removedById,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prizeIds);

        var raffleResult = await LoadRaffleOfEventAsync(eventId, cancellationToken);
        if (raffleResult.IsFailure)
            return UnitResult.Failure(raffleResult.Error);

        var raffle = raffleResult.Value;
        if (raffle.State == RaffleState.Completed)
            return UnitResult.Failure(RaffleErrors.Completed());

        var writeResult = await _repository.SetPrizesImageAsync(
            raffle.Id,
            prizeIds,
            imageFile: null,
            removedById,
            _clock.GetUtcNow(),
            cancellationToken);

        return FoundOr(writeResult, RaffleErrors.PrizeNotFound);
    }

    public async Task<UnitResult<BaseError>> SetOverrideAsync(SetAttendeeOverrideDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var key = NormalizeKey(dto.IdentityKey);

        var loadResult = await LoadDraftForWriteAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        var attendeeResult = await FindAtTheDoorAsync(dto.EventId, key, loadResult.Value.RequiresCheckIn, cancellationToken);
        if (attendeeResult.IsFailure)
            return UnitResult.Failure(attendeeResult.Error);

        if (dto.Decision == AttendeeOverrideDecision.Included)
        {
            var canInclude = await EnsureCanIncludeAsync(attendeeResult.Value, cancellationToken);
            if (canInclude.IsFailure)
                return canInclude;
        }

        var attendeeOverride = new AttendeeOverride(key, dto.Decision, dto.Note, dto.DecidedById, _clock.GetUtcNow());
        return await _repository.UpsertOverrideAsync(loadResult.Value.Id, attendeeOverride, cancellationToken);
    }

    public async Task<UnitResult<BaseError>> RemoveOverrideAsync(Guid eventId, string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var loadResult = await LoadDraftForWriteAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return UnitResult.Failure(loadResult.Error);

        return await _repository.DeleteOverrideAsync(loadResult.Value.Id, NormalizeKey(email), cancellationToken);
    }

    public async Task<Result<BulkExcludeOutcome, BaseError>> BulkExcludeAsync(BulkExcludeAttendeesDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if ((dto.SourceEventId is null) == (dto.Identifiers is null))
            return Result.Failure<BulkExcludeOutcome, BaseError>(RaffleErrors.BulkExcludeSourceRequired());
        if (dto.Identifiers is { Count: > BulkExclusion.MaxIdentifiers })
            return Result.Failure<BulkExcludeOutcome, BaseError>(RaffleErrors.BulkExcludeTooMany(BulkExclusion.MaxIdentifiers));
        if (dto.SourceEventId == dto.EventId)
            return Result.Failure<BulkExcludeOutcome, BaseError>(RaffleErrors.BulkExcludeSameEvent());

        var loadResult = await LoadDraftForWriteAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<BulkExcludeOutcome, BaseError>(loadResult.Error);
        var raffle = loadResult.Value;

        IReadOnlyList<BulkExclusion.Candidate> candidates;
        IReadOnlyList<string> invalid = [];
        IReadOnlyList<string> unidentifiable = [];
        string defaultNote;
        if (dto.SourceEventId is { } sourceEventId)
        {
            var source = await LoadSourceWinnersAsync(sourceEventId, cancellationToken);
            if (source.IsFailure)
                return Result.Failure<BulkExcludeOutcome, BaseError>(source.Error);
            (candidates, unidentifiable, defaultNote) = source.Value;
        }
        else
        {
            (candidates, invalid) = BulkExclusion.ReadIdentifiers(dto.Identifiers!);
            defaultNote = "Importado en lote";
        }

        var roster = await _repository.FindRosterPeopleAsync(
            dto.EventId,
            candidates.Where(c => c.Email is not null).Select(c => c.Email!).ToHashSet(StringComparer.Ordinal),
            candidates.Where(c => c.Id is not null).Select(c => c.Id!).ToHashSet(StringComparer.Ordinal),
            [],
            cancellationToken);
        if (roster.IsFailure)
            return Result.Failure<BulkExcludeOutcome, BaseError>(roster.Error);

        // La puerta se lee una sola vez para el lote entero, en vivo como en la exclusión de a uno.
        var door = await ReadDoorAsync(dto.EventId, raffle.RequiresCheckIn, cancellationToken);
        if (door.IsFailure)
            return Result.Failure<BulkExcludeOutcome, BaseError>(door.Error);

        var existing = await _repository.ListOverridesAsync(raffle.Id, cancellationToken);
        if (existing.IsFailure)
            return Result.Failure<BulkExcludeOutcome, BaseError>(existing.Error);

        var (outcome, keys) = BulkExclusion.Classify(
            candidates,
            invalid,
            unidentifiable,
            roster.Value,
            door.Value.Select(a => a.IdentityKey).ToHashSet(StringComparer.Ordinal),
            existing.Value);

        var now = _clock.GetUtcNow();
        var note = dto.Note ?? defaultNote;
        var write = await _repository.UpsertOverridesAsync(
            raffle.Id,
            keys.Select(k => new AttendeeOverride(k, AttendeeOverrideDecision.Excluded, note, dto.DecidedById, now)).ToList(),
            cancellationToken);

        return write.IsFailure
            ? Result.Failure<BulkExcludeOutcome, BaseError>(write.Error)
            : Result.Success<BulkExcludeOutcome, BaseError>(outcome);
    }

    /// <summary>
    /// Los ganadores vivos del sorteo de otro evento de la cuenta, como candidatos: el correo y el ID
    /// salen de su clave y del padrón de ese evento, en una sola consulta. Un evento de otra cuenta
    /// responde igual que uno sin sorteo. Un ganador anulado no se llevó nada y no cuenta.
    /// </summary>
    private async Task<Result<SourceWinners, BaseError>> LoadSourceWinnersAsync(
        Guid sourceEventId, CancellationToken cancellationToken)
    {
        // El repositorio de eventos filtra por cuenta: sin evento visible, sin sorteo.
        var sourceEvent = await _eventRepository.GetByIdAsync(sourceEventId, cancellationToken);
        if (sourceEvent.IsFailure)
            return Result.Failure<SourceWinners, BaseError>(sourceEvent.Error);
        if (sourceEvent.Value.HasNoValue)
            return Result.Failure<SourceWinners, BaseError>(RaffleErrors.NotFound());

        var sourceRaffle = await _repository.GetWithRoundsByEventIdAsync(sourceEventId, cancellationToken);
        if (sourceRaffle.IsFailure)
            return Result.Failure<SourceWinners, BaseError>(sourceRaffle.Error);
        if (sourceRaffle.Value.HasNoValue)
            return Result.Failure<SourceWinners, BaseError>(RaffleErrors.NotFound());

        var winners = sourceRaffle.Value.Value.Draws
            .Where(d => d.IsLive)
            .GroupBy(d => d.WinnerIdentityKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var people = await _repository.FindRosterPeopleAsync(
            sourceEventId, [], [], winners.Select(w => w.WinnerIdentityKey).ToList(), cancellationToken);
        if (people.IsFailure)
            return Result.Failure<SourceWinners, BaseError>(people.Error);
        var byKey = people.Value.ToDictionary(p => p.IdentityKey, StringComparer.Ordinal);

        var candidates = new List<BulkExclusion.Candidate>();
        var unidentifiable = new List<string>();
        foreach (var winner in winners)
        {
            byKey.TryGetValue(winner.WinnerIdentityKey, out var person);
            var email = person?.Email ?? PersonIdentity.EmailOf(winner.WinnerIdentityKey);
            var id = PersonIdentity.NormalizeId(person?.EmployeeCode) ?? PersonIdentity.IdOf(winner.WinnerIdentityKey);
            if (email is null && id is null)
                unidentifiable.Add(winner.WinnerName);
            else
                candidates.Add(new BulkExclusion.Candidate(email ?? person?.EmployeeCode?.Trim() ?? id!, email, id));
        }

        return Result.Success<SourceWinners, BaseError>(
            new SourceWinners(candidates, unidentifiable, $"Ganador de «{sourceEvent.Value.Value.Event.Name}»"));
    }

    private sealed record SourceWinners(
        IReadOnlyList<BulkExclusion.Candidate> Candidates,
        IReadOnlyList<string> Unidentifiable,
        string Note);

    /// <summary>
    /// Que el evento, el sorteo y el premio existan, y después todo lo demás bajo el bloqueo del
    /// sorteo (spec 02 §2). No pasa por <see cref="LoadForSetupAsync"/>: el estado del evento no frena
    /// el sorteo de un ganador (spec 00 §5). La hora, el código y el azar se toman adentro del bloqueo:
    /// un sorteo que esperó detrás de otro lleva una hora posterior, y el acta los lista en el orden
    /// en que ocurrieron (spec 02 §7).
    /// </summary>
    public async Task<Result<PrizeWithDraws, BaseError>> DrawAsync(
        Guid eventId,
        Guid prizeId,
        Guid drawnById,
        CancellationToken cancellationToken)
    {
        // Sin las rondas: con 10.000 premios leerlas costaba más que el giro (A31). Que el premio sea de
        // este sorteo lo comprueba el repositorio bajo el bloqueo, con el mismo PRIZE_NOT_FOUND.
        var loadResult = await LoadRaffleOfEventAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(loadResult.Error);

        var drawResult = await _repository.DrawAsync(
            loadResult.Value.Id,
            prizeId,
            (raffle, prize, attendees, draws) => PrizeDrawing.Draw(
                raffle, prize, attendees, draws, RandomNumberGenerator.GetInt32, drawnById,
                _clock.GetUtcNow(), RedemptionCode.Generate()),
            cancellationToken);
        if (drawResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(drawResult.Error);

        return await LoadPrizeWithDrawsAsync(eventId, prizeId, cancellationToken);
    }

    public async Task<Result<DrawBatchServiceOutcome, BaseError>> DrawBatchAsync(
        Guid eventId,
        IReadOnlyList<Guid> prizeIds,
        Guid drawnById,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prizeIds);

        var raffleResult = await LoadRaffleOfEventAsync(eventId, cancellationToken);
        if (raffleResult.IsFailure)
            return Result.Failure<DrawBatchServiceOutcome, BaseError>(raffleResult.Error);

        var batchResult = await _repository.DrawBatchAsync(
            raffleResult.Value.Id,
            prizeIds,
            (raffle, prize, attendees, draws) => PrizeDrawing.Draw(
                raffle, prize, attendees, draws, RandomNumberGenerator.GetInt32, drawnById,
                _clock.GetUtcNow(), RedemptionCode.Generate()),
            cancellationToken);
        if (batchResult.IsFailure)
            return Result.Failure<DrawBatchServiceOutcome, BaseError>(batchResult.Error);

        if (batchResult.Value.Drawn.Count == 0)
            return Result.Success<DrawBatchServiceOutcome, BaseError>(
                new DrawBatchServiceOutcome([], batchResult.Value.ExhaustedError));

        // Una sola recarga del detalle del sorteo en lugar de N llamadas a LoadPrizeWithDrawsAsync.
        var detailResult = await GetAsync(eventId, cancellationToken);
        if (detailResult.IsFailure)
            return Result.Failure<DrawBatchServiceOutcome, BaseError>(detailResult.Error);

        var detail = detailResult.Value.Raffle;
        var prizesById = detail.Rounds
            .SelectMany(r => r.Prizes)
            .ToDictionary(p => p.Id);

        var drawnPrizes = new List<PrizeWithDraws>(batchResult.Value.Drawn.Count);
        foreach (var draw in batchResult.Value.Drawn)
        {
            if (prizesById.TryGetValue(draw.PrizeId, out var prize))
            {
                drawnPrizes.Add(new PrizeWithDraws(
                    prize,
                    detail.DrawsOf(prize.Id),
                    detail.SponsorLogoOf(prize)));
            }
        }

        return Result.Success<DrawBatchServiceOutcome, BaseError>(
            new DrawBatchServiceOutcome(drawnPrizes, batchResult.Value.ExhaustedError));
    }

    /// <summary>
    /// Como <see cref="DrawAsync"/>: que existan el evento, el sorteo y el premio, y todo lo demás bajo
    /// el bloqueo del sorteo (spec 02 §3, §7). El estado del evento no frena una anulación (spec 00 §5).
    /// </summary>
    public async Task<Result<PrizeWithDraws, BaseError>> VoidAsync(VoidPrizeDrawDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadPrizeAsync(dto.EventId, dto.PrizeId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(loadResult.Error);

        var voidResult = await _repository.VoidAsync(
            loadResult.Value.Raffle.Id,
            dto.PrizeId,
            (raffle, prize, draws) => PrizeVoiding.Void(
                raffle, prize, draws, dto.Code, dto.Note, dto.VoidedById, _clock.GetUtcNow()),
            cancellationToken);
        if (voidResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(voidResult.Error);

        return await LoadPrizeWithDrawsAsync(dto.EventId, dto.PrizeId, cancellationToken);
    }

    /// <summary>
    /// En el orden de spec 03 §3: que existan el evento, el sorteo y el premio; la firma; los bytes; y
    /// todo lo demás bajo el bloqueo del sorteo. No hay comprobación del estado del premio antes de
    /// guardar: se decide una sola vez, bajo el bloqueo, y una entrega rechazada descarta sus bytes
    /// como cualquier subida cuyo enlace no se escribió (ADR-0014).
    /// </summary>
    public async Task<Result<PrizeWithDraws, BaseError>> HandOverAsync(HandOverPrizeDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadPrizeAsync(dto.EventId, dto.PrizeId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(loadResult.Error);

        var inspected = await _uploadPolicy.InspectSignatureAsync(
            dto.Signature, dto.SignatureFileName, dto.SignatureContentType, cancellationToken);
        if (inspected.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(inspected.Error);

        // Lo que se guarda y se vuelve a servir es el tipo que dicen los bytes.
        var contentType = inspected.Value.ContentType;

        StoredFileLocation location;
        try
        {
            location = await _fileStore.SaveAsync(dto.Signature, dto.SignatureFileName, contentType, cancellationToken);
        }
        catch (FileStoreUnavailableException)
        {
            return Result.Failure<PrizeWithDraws, BaseError>(FileErrors.FileStoreUnavailable());
        }

        var now = _clock.GetUtcNow();
        var signature = new StoredFile(
            id: Guid.NewGuid(),
            provider: location.Provider,
            storageKey: location.StorageKey,
            fileName: dto.SignatureFileName,
            contentType: contentType,
            sizeBytes: location.SizeBytes,
            checksum: location.Checksum,
            missingSince: null,
            createdAt: now,
            createdById: dto.RedeemedById);

        var handoverResult = await _repository.HandOverAsync(
            loadResult.Value.Raffle.Id,
            dto.PrizeId,
            signature,
            (raffle, prize, draws) => PrizeHandingOver.HandOver(
                raffle, prize, draws, dto.Receiver, signature.Id, dto.RedeemedById, _clock.GetUtcNow()),
            cancellationToken);
        if (handoverResult.IsFailure)
        {
            await _fileStore.DiscardOrphanBytesAsync(location.StorageKey);
            return Result.Failure<PrizeWithDraws, BaseError>(handoverResult.Error);
        }

        return await LoadPrizeWithDrawsAsync(dto.EventId, dto.PrizeId, cancellationToken);
    }

    /// <summary>
    /// Como <see cref="VoidAsync"/>: que existan el evento, el sorteo y el premio, y todo lo demás bajo
    /// el bloqueo del sorteo (spec 03 §3.3, §3.4).
    /// </summary>
    public async Task<Result<PrizeWithDraws, BaseError>> ReverseHandoverAsync(
        ReversePrizeHandoverDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadPrizeAsync(dto.EventId, dto.PrizeId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(loadResult.Error);

        var reverseResult = await _repository.ReverseHandoverAsync(
            loadResult.Value.Raffle.Id,
            dto.PrizeId,
            (raffle, prize, draws) => PrizeHandingOver.Reverse(
                raffle, prize, draws, dto.Reason, dto.ReversedById, _clock.GetUtcNow()),
            cancellationToken);
        if (reverseResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(reverseResult.Error);

        return await LoadPrizeWithDrawsAsync(dto.EventId, dto.PrizeId, cancellationToken);
    }

    public async Task<Result<PrizeDrawLookup, BaseError>> FindDrawByCodeAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        var canonical = RedemptionCode.TryNormalize(code);
        if (canonical is null)
            return Result.Failure<PrizeDrawLookup, BaseError>(RaffleErrors.RedemptionCodeNotFound());

        var found = await _repository.FindDrawByCodeAsync(canonical, cancellationToken);
        if (found.IsFailure)
            return Result.Failure<PrizeDrawLookup, BaseError>(found.Error);

        return found.Value.HasValue
            ? Result.Success<PrizeDrawLookup, BaseError>(found.Value.Value)
            : Result.Failure<PrizeDrawLookup, BaseError>(RaffleErrors.RedemptionCodeNotFound());
    }

    /// <summary>
    /// En el orden de spec 02 §5: que existan el evento, el sorteo y el premio; que el sorteo esté en
    /// curso —un completado también es <c>RAFFLE_NOT_IN_PROGRESS</c>, porque ya no hay sorteo que
    /// alimentar—; que el premio no tenga un ganador vivo; y el bombo. Sin bloqueo: el sorteo no se
    /// fía de esta lectura y la recalcula.
    /// </summary>
    public async Task<Result<EligiblePool, BaseError>> GetEligibleAsync(Guid eventId, Guid prizeId, CancellationToken cancellationToken)
    {
        var loadResult = await LoadPrizeAsync(eventId, prizeId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<EligiblePool, BaseError>(loadResult.Error);

        var withRounds = loadResult.Value;
        var raffle = withRounds.Raffle;
        if (raffle.State != RaffleState.InProgress)
            return Result.Failure<EligiblePool, BaseError>(RaffleErrors.NotInProgress());

        var liveDraw = withRounds.DrawsOf(prizeId).FirstOrDefault(d => d.IsLive);
        if (liveDraw is not null)
            return Result.Failure<EligiblePool, BaseError>(new PrizeAlreadyDrawnError(liveDraw));

        var attendees = await _repository.ListFrozenAttendeesAsync(raffle.Id, cancellationToken);
        if (attendees.IsFailure)
            return Result.Failure<EligiblePool, BaseError>(attendees.Error);

        return EligiblePool.For(attendees.Value, withRounds.Draws, raffle.OnePrizePerCompany);
    }

    public async Task<Result<RaffleDetail, BaseError>> ReinstateAttendeeAsync(
        Guid eventId,
        Guid attendeeId,
        Guid reinstatedById,
        CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(loadResult.Error);

        if (!EventStatus.Active.Contains(statusResult.Value))
            return Result.Failure<RaffleDetail, BaseError>(QuoteErrors.EventNoEditable());

        var writeResult = await _repository.ReinstateAttendeeAsync(
            loadResult.Value.Id,
            attendeeId,
            reinstatedById,
            _clock.GetUtcNow(),
            cancellationToken);
        if (writeResult.IsFailure)
            return Result.Failure<RaffleDetail, BaseError>(writeResult.Error);

        return await GetAsync(eventId, cancellationToken);
    }

    /// <summary>
    /// El premio con su estado y sus sorteos, releído después de escribir en él. Lee solo ese premio,
    /// no el sorteo entero (A31).
    /// </summary>
    private async Task<Result<PrizeWithDraws, BaseError>> LoadPrizeWithDrawsAsync(
        Guid eventId,
        Guid prizeId,
        CancellationToken cancellationToken)
    {
        var raffleResult = await LoadRaffleOfEventAsync(eventId, cancellationToken);
        if (raffleResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(raffleResult.Error);

        var prizeResult = await _repository.GetPrizeWithDrawsAsync(raffleResult.Value.Id, prizeId, cancellationToken);
        if (prizeResult.IsFailure)
            return Result.Failure<PrizeWithDraws, BaseError>(prizeResult.Error);

        return prizeResult.Value.HasValue
            ? prizeResult.Value.Value
            : Result.Failure<PrizeWithDraws, BaseError>(RaffleErrors.PrizeNotFound());
    }

    /// <summary>
    /// El sorteo, sin rondas, si el evento existe y tiene sorteo, en ese orden. El estado del evento
    /// no se mira.
    /// </summary>
    private async Task<Result<Raffle, BaseError>> LoadRaffleOfEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        return statusResult.IsFailure
            ? Result.Failure<Raffle, BaseError>(statusResult.Error)
            : await LoadAsync(eventId, cancellationToken);
    }

    /// <summary>
    /// El sorteo con sus rondas y sorteos, si el evento existe, tiene sorteo y el premio es de ese
    /// sorteo, en ese orden. El estado del evento no se mira.
    /// </summary>
    private async Task<Result<RaffleWithRounds, BaseError>> LoadPrizeAsync(Guid eventId, Guid prizeId, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleWithRounds, BaseError>(statusResult.Error);

        var loadResult = await LoadWithRoundsAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return loadResult;

        var prizeResult = loadResult.Value.FindPrize(prizeId);
        return prizeResult.IsFailure ? Result.Failure<RaffleWithRounds, BaseError>(prizeResult.Error) : loadResult;
    }

    /// <summary>
    /// El sorteo, listo para una escritura que solo admite el borrador —una decisión manual o el
    /// arranque—: que el evento exista, que tenga sorteo, que admita escrituras y que el sorteo siga
    /// en borrador, en ese orden (spec 01 §9, §9.1, pasos 2 a 5).
    /// </summary>
    private async Task<Result<Raffle, BaseError>> LoadDraftForWriteAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<Raffle, BaseError>(statusResult.Error);

        var loadResult = await LoadAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return loadResult;

        if (!EventStatus.Active.Contains(statusResult.Value))
            return Result.Failure<Raffle, BaseError>(QuoteErrors.EventNoEditable());

        var isDraft = loadResult.Value.EnsureDraft();
        return isDraft.IsFailure ? Result.Failure<Raffle, BaseError>(isDraft.Error) : loadResult;
    }

    /// <summary>
    /// La persona en la puerta, leída en vivo y nunca de la copia: una escritura valida contra el
    /// dueño del dato y falla cerrada (ADR-0013). El techo se cuenta igual que en la lectura.
    /// </summary>
    private async Task<Result<Attendee, BaseError>> FindAtTheDoorAsync(
        Guid eventId, string identityKey, bool requiresCheckIn, CancellationToken cancellationToken)
    {
        var door = await ReadDoorAsync(eventId, requiresCheckIn, cancellationToken);
        if (door.IsFailure)
            return Result.Failure<Attendee, BaseError>(door.Error);

        var attendee = door.Value.FirstOrDefault(a => a.IdentityKey == identityKey);
        return attendee is null
            ? Result.Failure<Attendee, BaseError>(RaffleErrors.ParticipantNotCheckedIn())
            : Result.Success<Attendee, BaseError>(attendee);
    }

    /// <summary>La puerta en vivo, respetando <c>RequiresCheckIn</c> y el techo de asistentes.</summary>
    private async Task<Result<IReadOnlyList<Attendee>, BaseError>> ReadDoorAsync(
        Guid eventId, bool requiresCheckIn, CancellationToken cancellationToken)
    {
        var door = requiresCheckIn
            ? await _liveAttendance.GetCheckedInAsync(eventId, cancellationToken)
            : await _liveAttendance.GetAttendeesAsync(eventId, requiresCheckIn: false, cancellationToken);
        if (door.IsFailure)
            return door;

        return door.Value.Count > MaxAttendees
            ? Result.Failure<IReadOnlyList<Attendee>, BaseError>(RaffleErrors.AttendanceTooManyRows())
            : door;
    }

    /// <summary>
    /// Que una inclusión no meta en el bombo a nadie que el pipeline deja fuera sin remedio: los
    /// pasos 1 a 4 de spec 01 §6, en ese orden. Al CRM se le pregunta solo al final, y solo por este
    /// contacto. Una exclusión no pasa por acá: nunca mete a nadie.
    /// </summary>
    private async Task<UnitResult<BaseError>> EnsureCanIncludeAsync(Attendee attendee, CancellationToken cancellationToken)
    {
        // En sorteos corporativos sin CRM obligatorio, los participantes sin ContactId o agregados
        // en puerta califican salvo veto explícito del CRM cuando sí tienen contacto asociado.
        if (attendee.ContactId is not { } contactId)
            return UnitResult.Success<BaseError>();

        var verdicts = await _mailability.CheckAsync([contactId], cancellationToken);
        if (verdicts.IsFailure)
            return UnitResult.Failure(verdicts.Error);

        var classified = Qualification.Classify(
            [attendee], Maybe<AwardsList>.None, [], Maybe.From(verdicts.Value)).Attendees[0];

        return classified.ExclusionReason switch
        {
            ExclusionReason.Suspended => UnitResult.Failure(RaffleErrors.ParticipantSuspended()),
            ExclusionReason.DoNotInvite => UnitResult.Failure(RaffleErrors.ParticipantDoNotInvite()),
            _ => UnitResult.Success<BaseError>(),
        };
    }

    /// <summary>
    /// La clave de la persona tal como llega (un correo o una <c>identityKey</c>): sin espacios
    /// alrededor y en minúsculas, el mismo subconjunto conservador que usa la puerta (spec 01 §2).
    /// Nada más: ni puntos de gmail ni <c>+etiqueta</c>. Las claves ya se guardan así.
    /// </summary>
    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    /// <summary>
    /// Si el sorteo puede arrancar (spec 01 §8.1). Sin copia de la puerta se informa solo
    /// <c>attendance_unavailable</c>: lo desconocido no es vacío, y decir además
    /// <c>no_qualified_attendees</c> sería afirmar algo que no se sabe. Pasar del techo bloquea pero
    /// no tumba la lectura, para que el conteo de la puerta no deje en blanco la pantalla de armado.
    /// Con el CRM caído tampoco: se clasifica sin la lista negra y se bloquea con
    /// <c>crm_unavailable</c>.
    /// </summary>
    private async Task<Result<RaffleReadiness, BaseError>> GetReadinessAsync(RaffleWithRounds raffle, CancellationToken cancellationToken)
    {
        var noPrizes = raffle.Rounds.All(r => r.Prizes.Count == 0);

        // Fuera del borrador no hay arranque para el que estar listo ni nada que leer: el aviso de
        // DO Travel Awards sale de lo que quedó escrito al arrancar, que es lo que dirá el acta.
        if (raffle.Raffle.State != RaffleState.Draft)
        {
            var frozenWarnings = new List<string>();
            if (raffle.Raffle.AwardsBatchId is null)
                frozenWarnings.Add(RaffleWarning.AwardsListNotInForce);
            if (noPrizes)
                frozenWarnings.Add(RaffleWarning.NoPrizes);

            return Result.Success<RaffleReadiness, BaseError>(new RaffleReadiness(null, null, frozenWarnings));
        }

        var drumResult = await ComputeDrumAsync(raffle.Raffle, cancellationToken);
        if (drumResult.IsFailure)
            return Result.Failure<RaffleReadiness, BaseError>(drumResult.Error);

        var drum = drumResult.Value;
        var blockingReasons = new List<string>();
        if (drum.Attendance.Attendees is null)
            blockingReasons.Add(RaffleBlockingReason.AttendanceUnavailable);
        else if (drum.TooManyRows)
            blockingReasons.Add(RaffleBlockingReason.AttendanceTooManyRows);
        else if (drum.Qualification!.Qualified == 0)
            blockingReasons.Add(RaffleBlockingReason.NoQualifiedAttendees);

        if (drum.CrmUnavailable)
            blockingReasons.Add(RaffleBlockingReason.CrmUnavailable);

        var warnings = new List<string>();
        if (drum.AwardsList.HasNoValue)
            warnings.Add(RaffleWarning.AwardsListNotInForce);
        if (drum.Attendance.Status == AttendanceStatus.Stale)
            warnings.Add(RaffleWarning.AttendanceCopyStale);
        if (noPrizes)
            warnings.Add(RaffleWarning.NoPrizes);

        return Result.Success<RaffleReadiness, BaseError>(new RaffleReadiness(blockingReasons.Count == 0, blockingReasons, warnings));
    }

    /// <summary>
    /// Calcula el bombo sobre la copia reciente de la puerta, la lista de DO Travel Awards en vigor,
    /// las decisiones manuales y lo que el CRM conteste en este momento. No escribe nada: antes de
    /// arrancar no hay nada guardado que recalcular (spec 01 §4).
    /// <para>
    /// La clasificación queda nula sin copia de la puerta y también por encima del techo, donde
    /// quien llama rechaza; en los dos casos no se le pregunta nada al CRM.
    /// </para>
    /// <para>
    /// Al CRM se le pregunta en cada lectura, sin copia (spec 01 §8), y su respuesta se informa en
    /// <see cref="CrmDirectoryStatusContext"/>: <c>ok</c> si contestó, <c>unavailable</c> si no, y nada
    /// si no había a quién preguntar. Sin respuesta se clasifica igual, salteando la lista negra:
    /// dejar la pantalla en blanco por un tropiezo del CRM sería peor, y arrancar queda bloqueado.
    /// </para>
    /// </summary>
    private async Task<Result<Drum, BaseError>> ComputeDrumAsync(Raffle raffle, CancellationToken cancellationToken)
    {
        var attendance = raffle.RequiresCheckIn
            ? await _attendance.GetAsync(raffle.EventId, cancellationToken)
            : await _attendance.GetAsync(raffle.EventId, requiresCheckIn: false, cancellationToken);
        var awardsList = await _awards.GetInForceAsync(cancellationToken);

        var tooManyRows = attendance.Attendees is { Count: > MaxAttendees };
        if (attendance.Attendees is null || tooManyRows)
            return Result.Success<Drum, BaseError>(new Drum(attendance, awardsList, null, tooManyRows, CrmUnavailable: false));

        var overrides = await _repository.ListOverridesAsync(raffle.Id, cancellationToken);
        if (overrides.IsFailure)
            return Result.Failure<Drum, BaseError>(overrides.Error);

        var mailability = await AskMailabilityAsync(attendance.Attendees, cancellationToken);

        return Result.Success<Drum, BaseError>(new Drum(
            attendance,
            awardsList,
            Qualification.Classify(attendance.Attendees, awardsList, overrides.Value, mailability),
            TooManyRows: false,
            CrmUnavailable: mailability.HasNoValue));
    }

    /// <summary>
    /// Lo que el CRM dice de los contactos de la puerta, o <c>None</c> si no contestó. Sin ningún
    /// contacto que preguntar no se llama ni se informa nada: decir <c>ok</c> sobre una consulta que
    /// no se hizo es peor que no decir nada.
    /// </summary>
    private async Task<Maybe<IReadOnlyDictionary<Guid, string?>>> AskMailabilityAsync(
        IReadOnlyList<Attendee> attendees,
        CancellationToken cancellationToken)
    {
        var contactIds = DistinctContactIds(attendees);
        if (contactIds.Count == 0)
            return Maybe.From<IReadOnlyDictionary<Guid, string?>>(new Dictionary<Guid, string?>());

        var verdicts = await _mailability.CheckAsync(contactIds, cancellationToken);
        if (verdicts.IsFailure)
        {
            _crmStatus.Report(CrmDirectoryStatus.Unavailable);
            return Maybe<IReadOnlyDictionary<Guid, string?>>.None;
        }

        _crmStatus.Report(CrmDirectoryStatus.Ok);
        return Maybe.From(verdicts.Value);
    }

    /// <summary>
    /// Lo que el CRM dice de los contactos de la puerta, para el arranque: sin degradar, porque un
    /// bombo congelado sin la lista negra no tiene arreglo. Sin ningún contacto que preguntar no se
    /// llama, igual que en la lectura.
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<Guid, string?>, BaseError>> AskMailabilityLiveAsync(
        IReadOnlyList<Attendee> attendees,
        CancellationToken cancellationToken)
    {
        var contactIds = DistinctContactIds(attendees);
        if (contactIds.Count == 0)
            return Result.Success<IReadOnlyDictionary<Guid, string?>, BaseError>(new Dictionary<Guid, string?>());

        return await _mailability.CheckAsync(contactIds, cancellationToken);
    }

    /// <summary>Los contactos de la puerta, cada uno una vez: dos registros pueden compartir uno.</summary>
    private static IReadOnlyList<Guid> DistinctContactIds(IReadOnlyList<Attendee> attendees) => attendees
        .Where(a => a.ContactId is not null)
        .Select(a => a.ContactId!.Value)
        .Distinct()
        .ToList();

    /// <summary>
    /// Por apellido, nombre y correo: un operador busca un nombre, y el orden tiene que quedarse
    /// quieto mientras la pantalla consulta. El mismo a los dos lados del congelado.
    /// </summary>
    private static IReadOnlyList<RaffleAttendee> InDisplayOrder(IEnumerable<RaffleAttendee> attendees) => attendees
        .OrderBy(a => a.LastName, NameOrder)
        .ThenBy(a => a.FirstName, NameOrder)
        .ThenBy(a => a.IdentityKey, StringComparer.Ordinal)
        .ToList();

    /// <summary>Lo que se leyó para calcular el bombo y lo que salió.</summary>
    private sealed record Drum(
        RecentAttendance Attendance,
        Maybe<AwardsList> AwardsList,
        QualificationResult? Qualification,
        bool TooManyRows,
        bool CrmUnavailable);

    /// <summary>
    /// El sorteo con sus rondas, listo para cambiar su armado. Corre el orden de comprobaciones de
    /// todo el servicio —que el evento exista, que tenga sorteo, que admita escrituras— y después
    /// exige que el sorteo no esté completado.
    /// </summary>
    private async Task<Result<RaffleWithRounds, BaseError>> LoadForSetupAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var statusResult = await GetEventStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<RaffleWithRounds, BaseError>(statusResult.Error);

        var loadResult = await LoadWithRoundsAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return loadResult;

        if (!EventStatus.Active.Contains(statusResult.Value))
            return Result.Failure<RaffleWithRounds, BaseError>(QuoteErrors.EventNoEditable());

        var canChange = loadResult.Value.EnsureCanChangeSetup();
        return canChange.IsFailure
            ? Result.Failure<RaffleWithRounds, BaseError>(canChange.Error)
            : loadResult;
    }

    /// <summary>
    /// El sorteo con sus rondas y premios, o <c>RAFFLE_NOT_FOUND</c> si el evento todavía no tiene.
    /// </summary>
    private async Task<Result<RaffleWithRounds, BaseError>> LoadWithRoundsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var loadResult = await _repository.GetWithRoundsByEventIdAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<RaffleWithRounds, BaseError>(loadResult.Error);
        if (loadResult.Value.HasNoValue)
            return Result.Failure<RaffleWithRounds, BaseError>(RaffleErrors.NotFound());

        return Result.Success<RaffleWithRounds, BaseError>(loadResult.Value.Value);
    }

    /// <summary>
    /// Traduce el <c>false</c> del repositorio —la fila se fue entre la lectura y la escritura— al
    /// no-encontrado de lo que se estaba tocando.
    /// </summary>
    private static UnitResult<BaseError> FoundOr(Result<bool, BaseError> writeResult, Func<BaseError> notFound)
    {
        if (writeResult.IsFailure)
            return UnitResult.Failure(writeResult.Error);

        return writeResult.Value ? UnitResult.Success<BaseError>() : UnitResult.Failure(notFound());
    }

    /// <summary>El sorteo del evento, o <c>RAFFLE_NOT_FOUND</c> si todavía no tiene.</summary>
    private async Task<Result<Raffle, BaseError>> LoadAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var loadResult = await _repository.GetByEventIdAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<Raffle, BaseError>(loadResult.Error);
        if (loadResult.Value.HasNoValue)
            return Result.Failure<Raffle, BaseError>(RaffleErrors.NotFound());

        return Result.Success<Raffle, BaseError>(loadResult.Value.Value);
    }

    /// <summary>
    /// El estado del evento, o <c>EVENT_NOT_FOUND</c> si no existe o fue eliminado.
    /// </summary>
    private async Task<Result<string, BaseError>> GetEventStatusAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var statusResult = await _eventRepository.GetStatusAsync(eventId, cancellationToken);
        if (statusResult.IsFailure)
            return Result.Failure<string, BaseError>(statusResult.Error);
        if (statusResult.Value.HasNoValue)
            return Result.Failure<string, BaseError>(EventImageErrors.EventNotFound());

        return Result.Success<string, BaseError>(statusResult.Value.Value);
    }
}

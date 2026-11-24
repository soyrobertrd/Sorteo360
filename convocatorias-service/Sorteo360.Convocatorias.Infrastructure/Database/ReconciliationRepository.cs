using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Reconciliation;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="IReconciliationRepository" />
public sealed class ReconciliationRepository : IReconciliationRepository
{
    /// <summary>Tope duro del tamaño de página, el mismo del listado de campañas.</summary>
    private const int MaxPageSize = 100;

    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<ReconciliationRepository> _logger;
    private readonly TimeProvider _clock;
    private readonly ReconciliationSettings _settings;

    public ReconciliationRepository(
        ConvocatoriasDbContext context,
        ILogger<ReconciliationRepository> logger,
        TimeProvider clock,
        ReconciliationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(settings);

        _context = context;
        _logger = logger;
        _clock = clock;
        _settings = settings;
    }

    /// <inheritdoc />
    public Task<Result<ReconciliationCounts, BaseError>> CountAsync(CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "contar los cubos de reconciliación", async () =>
        {
            var registrations = _context.Registrations.AsNoTracking();

            return new ReconciliationCounts(
                PendingRegistrationSync: await registrations.CountAsync(PendingSync, cancellationToken),
                PendingUnsubscribes: await _context.CampaignRecipients.AsNoTracking()
                    .CountAsync(PendingUnsubscribe, cancellationToken),
                DetailsDiffer: await registrations.CountAsync(DetailsDifferUnreviewed, cancellationToken),
                StrandedCampaigns: await StrandedCampaigns().CountAsync(cancellationToken),
                UncancelledNotices: null);
        });

    /// <inheritdoc />
    public Task<Result<int, BaseError>> CountNeedsAttentionAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "contar lo que pide atención en el evento", async () =>
        {
            var registrations = _context.Registrations.AsNoTracking()
                .Where(registration => registration.EventId == eventId);

            return await registrations.CountAsync(PendingSync, cancellationToken)
                + await registrations.CountAsync(DetailsDifferUnreviewed, cancellationToken)
                + await StrandedCampaigns().CountAsync(campaign => campaign.EventId == eventId, cancellationToken);
        });

    /// <inheritdoc />
    public Task<Result<PagedResult<RegistrationReviewRow>, BaseError>> ListPendingRegistrationSyncAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "listar las inscripciones pendientes de sincronizar", () =>
            PageAsync(
                _context.Registrations.AsNoTracking()
                    .Where(PendingSync)
                    .OrderBy(registration => registration.CrmSyncFailedAt)
                    .ThenBy(registration => registration.Id)
                    .Select(ToReviewRow),
                page,
                pageSize,
                cancellationToken));

    /// <inheritdoc />
    public Task<Result<PagedResult<RegistrationReviewRow>, BaseError>> ListDetailsDifferAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "listar las fichas que no coinciden", () =>
            PageAsync(
                _context.Registrations.AsNoTracking()
                    .Where(DetailsDifferUnreviewed)
                    .OrderBy(registration => registration.CreatedAt)
                    .ThenBy(registration => registration.Id)
                    .Select(ToReviewRow),
                page,
                pageSize,
                cancellationToken));

    /// <inheritdoc />
    public async Task<Result<Maybe<RegistrationReviewRow>, BaseError>> AcknowledgeDetailsAsync(
        Guid registrationId,
        Guid reviewerId,
        CancellationToken cancellationToken)
    {
        var acknowledged = await DbResult.TryAsync(_logger, "dar por revisada la ficha", async () =>
        {
            var now = _clock.GetUtcNow();

            // La condición viaja dentro del UPDATE: dos revisiones a la vez no se pisan el sello, y
            // la primera es la que queda. Una ficha que coincide no se toca.
            await _context.Registrations
                .Where(registration => registration.Id == registrationId
                    && registration.ContactDetailsDiffer
                    && registration.DetailsReviewedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(registration => registration.DetailsReviewedAt, now)
                        .SetProperty(registration => registration.DetailsReviewedById, reviewerId)
                        .SetProperty(registration => registration.UpdatedAt, now),
                    cancellationToken);

            var row = await _context.Registrations.AsNoTracking()
                .Where(registration => registration.Id == registrationId)
                .Select(ToReviewRow)
                .FirstOrDefaultAsync(cancellationToken);

            return row is null ? Maybe<RegistrationReviewRow>.None : Maybe.From(row);
        });

        return acknowledged.Bind(row => row.HasValue && !row.Value.ContactDetailsDiffer
            ? Result.Failure<Maybe<RegistrationReviewRow>, BaseError>(new RegistrationDetailsDoNotDifferError())
            : Result.Success<Maybe<RegistrationReviewRow>, BaseError>(row));
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<PendingUnsubscribeRow>, BaseError>> ListPendingUnsubscribesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "listar las bajas pendientes", () =>
            PageAsync(
                _context.CampaignRecipients.AsNoTracking()
                    .Where(PendingUnsubscribe)
                    .OrderBy(recipient => recipient.UnsubscribedAt)
                    .ThenBy(recipient => recipient.Id)
                    .Select(recipient => new PendingUnsubscribeRow(
                        recipient.Id,
                        recipient.CampaignId,
                        recipient.Campaign!.EventId,
                        recipient.ContactId,
                        recipient.FirstName,
                        recipient.LastName,
                        recipient.Email,
                        recipient.UnsubscribedAt!.Value)),
                page,
                pageSize,
                cancellationToken));

    /// <inheritdoc />
    public Task<Result<Maybe<PagedResult<CampaignExclusionRow>>, BaseError>> ListExclusionsAsync(
        Guid campaignId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "listar las exclusiones de la campaña", async () =>
        {
            if (!await _context.Campaigns.AnyAsync(campaign => campaign.Id == campaignId, cancellationToken))
                return Maybe<PagedResult<CampaignExclusionRow>>.None;

            return Maybe.From(await PageAsync(
                _context.CampaignExclusions.AsNoTracking()
                    .Where(exclusion => exclusion.CampaignId == campaignId)
                    .OrderBy(exclusion => exclusion.LastName)
                    .ThenBy(exclusion => exclusion.FirstName)
                    .ThenBy(exclusion => exclusion.Id)
                    .Select(exclusion => new CampaignExclusionRow(
                        exclusion.Id,
                        exclusion.ContactId,
                        exclusion.FirstName,
                        exclusion.LastName,
                        exclusion.Email,
                        exclusion.Reason,
                        exclusion.ExcludedAt)),
                page,
                pageSize,
                cancellationToken));
        });

    /// <inheritdoc />
    public Task<Result<PagedResult<StrandedCampaignRow>, BaseError>> ListStrandedCampaignsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "listar las campañas varadas", () =>
            PageAsync(
                StrandedCampaigns()
                    .OrderBy(campaign => campaign.LaunchedAt)
                    .ThenBy(campaign => campaign.Id)
                    .Select(campaign => new StrandedCampaignRow(
                        campaign.Id,
                        campaign.EventId,
                        campaign.TemplateKey,
                        campaign.LaunchedAt!.Value,
                        new RecipientStatusSplit(
                            campaign.Recipients.Count(r => r.SendStatus == RecipientSendStatus.Pending),
                            campaign.Recipients.Count(r => r.SendStatus == RecipientSendStatus.HandedOff),
                            campaign.Recipients.Count(r => r.SendStatus == RecipientSendStatus.Failed),
                            campaign.Recipients.Count(r => r.SendStatus == RecipientSendStatus.Suppressed)))),
                page,
                pageSize,
                cancellationToken));

    /// <inheritdoc />
    public Task<Result<IReadOnlyDictionary<Guid, int>, BaseError>> CountUnnotifiedByEventAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyDictionary<Guid, int>>(_logger, "contar los avisos de cancelación pendientes", async () =>
            await _context.Registrations.AsNoTracking()
                .Where(registration => eventIds.Contains(registration.EventId)
                    && registration.CancellationNotifiedAt == null)
                .GroupBy(registration => registration.EventId)
                .Select(group => new { EventId = group.Key, Unnotified = group.Count() })
                .ToDictionaryAsync(group => group.EventId, group => group.Unnotified, cancellationToken));

    /// <summary>
    /// Varada, a propósito tonta (spec <c>07</c> §6.3): en <c>sending</c>, con alguna fila
    /// <c>pending</c> y lanzada antes del umbral. Sin tabla de arriendos ni latido. Se le escapa a
    /// sabiendas una <c>sent</c> que <c>retry-failed</c> reabrió y cuyo worker murió enseguida;
    /// volver a apretar <c>retry-failed</c> la cierra.
    /// </summary>
    private IQueryable<CampaignData> StrandedCampaigns()
    {
        var launchedBefore = _clock.GetUtcNow() - _settings.StrandedCampaignAfter;

        return _context.Campaigns.AsNoTracking()
            .Where(campaign => campaign.State == CampaignState.Sending
                && campaign.LaunchedAt < launchedBefore
                && campaign.Recipients.Any(r => r.SendStatus == RecipientSendStatus.Pending));
    }

    /// <summary>
    /// La misma condición que vacía el lanzamiento (spec <c>04</c> §3.1): dada de baja acá y sin el
    /// sello de que el CRM lo sabe.
    /// </summary>
    private static readonly Expression<Func<CampaignRecipientData, bool>> PendingUnsubscribe =
        recipient => recipient.UnsubscribedAt != null && recipient.UnsubscribeSyncedAt == null;

    /// <summary>
    /// Pendiente de sincronizar, derivado de las dos columnas anulables y de nada más
    /// (spec <c>06</c> §4.3): sin contacto y con el sello del fallo.
    /// </summary>
    private static readonly Expression<Func<RegistrationData, bool>> PendingSync =
        registration => registration.CrmContactId == null && registration.CrmSyncFailedAt != null;

    /// <summary>
    /// No coincide y nadie la revisó. La marca no se borra al revisar: lo que la saca del cubo es el
    /// sello, porque el arreglo es del CRM y este servicio no vuelve a leerlo (spec <c>07</c> §6.1).
    /// </summary>
    private static readonly Expression<Func<RegistrationData, bool>> DetailsDifferUnreviewed =
        registration => registration.ContactDetailsDiffer && registration.DetailsReviewedAt == null;

    private static readonly Expression<Func<RegistrationData, RegistrationReviewRow>> ToReviewRow =
        registration => new RegistrationReviewRow(
            registration.Id,
            registration.EventId,
            registration.FirstName,
            registration.LastName,
            registration.Email,
            registration.Reference,
            registration.CrmContactId,
            registration.CrmSyncFailedAt,
            registration.ContactDetailsDiffer,
            registration.DetailsReviewedAt,
            registration.DetailsReviewedById,
            registration.CreatedAt);

    /// <summary>Una página de una consulta ya ordenada, con su total.</summary>
    private static async Task<PagedResult<T>> PageAsync<T>(
        IQueryable<T> ordered,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<T>(items, total, page, pageSize);
    }
}

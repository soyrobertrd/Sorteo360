using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Unsubscribe;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="ICampaignRepository" />
public sealed class CampaignRepository : ICampaignRepository
{
    /// <summary>Tope duro del tamaño de página, igual que el del CRM.</summary>
    private const int MaxPageSize = 100;

    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<CampaignRepository> _logger;

    public CampaignRepository(ConvocatoriasDbContext context, ILogger<CampaignRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<CampaignSummary>, BaseError>> ListAsync(
        CampaignQuery query,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync<PagedResult<CampaignSummary>>(_logger, "listar las campañas", async () =>
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            var campaigns = _context.Campaigns.AsNoTracking();

            if (query.EventId is { } eventId)
                campaigns = campaigns.Where(c => c.EventId == eventId);

            if (query.LaunchedOnly)
                campaigns = campaigns.Where(LaunchedCampaign.Is);

            // Un estado que no pertenece al catálogo no devuelve todo: devuelve nada. Colar un
            // filtro que no se entiende es la forma de que alguien crea que no hay campañas en
            // "sendinng" cuando lo que hay es una errata.
            if (!string.IsNullOrWhiteSpace(query.State))
            {
                var state = CampaignState.Canonicalize(query.State);
                campaigns = state is null
                    ? campaigns.Where(_ => false)
                    : campaigns.Where(c => c.State == state);
            }

            var total = await campaigns.CountAsync(cancellationToken);

            var items = await campaigns
                .OrderByDescending(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CampaignProjection(c, c.Recipients.Count, c.Exclusions.Count))
                .ToListAsync(cancellationToken);

            return new PagedResult<CampaignSummary>(
                [.. items.Select(ToSummary)],
                total,
                page,
                pageSize);
        });

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignSummary>, BaseError>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "obtener la campaña", async () =>
        {
            var projection = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CampaignProjection(c, c.Recipients.Count, c.Exclusions.Count))
                .FirstOrDefaultAsync(cancellationToken);

            return projection is null
                ? Maybe<CampaignSummary>.None
                : Maybe.From(ToSummary(projection));
        });

    /// <inheritdoc />
    public Task<Result<Guid, BaseError>> CreateAsync(CreateCampaignDto dto, CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "crear la campaña", async () =>
        {
            if (CampaignDirectMessage.Check(dto.EventId, dto.TemplateKey, dto.Subject, dto.Message) is { } mismatch)
                return Result.Failure<Guid, BaseError>(mismatch);

            if (!await TemplateExistsAsync(dto.TemplateKey, dto.Language, cancellationToken))
            {
                return Result.Failure<Guid, BaseError>(
                    new CampaignTemplateNotFoundError(dto.TemplateKey, dto.Language));
            }

            var now = DateTimeOffset.UtcNow;

            var data = new CampaignData
            {
                Id = Guid.CreateVersion7(),
                EventId = dto.EventId,
                Subject = dto.Subject,
                Message = dto.Message,
                TemplateKey = dto.TemplateKey,
                Language = dto.Language,
                State = CampaignState.Draft,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = dto.CreatedById,
                UpdatedById = dto.CreatedById,
            };

            _context.Campaigns.Add(data);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<Guid, BaseError>(data.Id);
        });

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignSummary>, BaseError>> UpdateAsync(
        UpdateCampaignDto dto,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "actualizar la campaña", async () =>
        {
            var draft = await CampaignDraftGuard.LoadAsync<CampaignSummary>(_context, dto.Id, cancellationToken);

            if (draft.Campaign is not { } campaign)
                return draft.Refusal;

            // Es un PATCH: lo que no viene no se toca. Exigir las dos lo volvería un PUT, y una
            // pantalla que solo cambia el idioma tendría que reenviar la plantilla para no
            // borrarla.
            // La pareja se comprueba ya resuelta, con lo que no viene tomado de la fila: cambiar
            // solo el idioma puede dejar sin plantilla a una clave que en español sí la tenía.
            // Va después de la guarda de borrador, así que una campaña ya lanzada sigue
            // contestando CAMPAIGN_NOT_DRAFT y no esto.
            var templateKey = dto.TemplateKey ?? campaign.TemplateKey;
            var language = dto.Language ?? campaign.Language;
            var subject = dto.Subject ?? campaign.Subject;
            var message = dto.Message ?? campaign.Message;

            // El evento no se edita, así que cambiar de plantilla no puede convertir una campaña de
            // evento en un mensaje directo ni al revés, y el asunto y el texto solo se le editan a
            // un mensaje directo.
            if (CampaignDirectMessage.Check(campaign.EventId, templateKey, subject, message) is { } mismatch)
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(mismatch);

            if ((dto.TemplateKey is not null || dto.Language is not null)
                && !await TemplateExistsAsync(templateKey, language, cancellationToken))
            {
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(
                    new CampaignTemplateNotFoundError(templateKey, language));
            }

            campaign.TemplateKey = templateKey;
            campaign.Language = language;
            campaign.Subject = subject;
            campaign.Message = message;

            campaign.UpdatedAt = DateTimeOffset.UtcNow;
            campaign.UpdatedById = dto.UpdatedById;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<Maybe<CampaignSummary>, BaseError>(
                Maybe.From(await LoadSummaryAsync(campaign.Id, cancellationToken)));
        });

    /// <inheritdoc />
    public Task<Result<Maybe<RecipientSelection>, BaseError>> SelectRecipientsAsync(
        Guid campaignId,
        IReadOnlyList<CrmContact> contacts,
        CancellationToken cancellationToken)
    {
        // Fuera de la lambda: un nulo acá es un error de programación, y dentro saldría
        // disfrazado de fallo de base de datos.
        ArgumentNullException.ThrowIfNull(contacts);

        return DbResult.TryResultAsync(_logger, "seleccionar los destinatarios", async () =>
        {
            var draft = await CampaignDraftGuard.LoadAsync<RecipientSelection>(_context, campaignId, cancellationToken);

            if (draft.Campaign is not { } campaign)
                return draft.Refusal;

            // Quién ya está como destinatario. Una persona está una vez en la campaña, así que
            // este conjunto es lo que hace que seleccionar dos veces componga en vez de duplicar.
            var alreadyRecipients = new HashSet<Guid>(
                await _context.CampaignRecipients
                    .Where(r => r.CampaignId == campaignId)
                    .Select(r => r.ContactId)
                    .ToListAsync(cancellationToken));

            // Las exclusiones se cargan enteras y no solo sus ids: una exclusión no es
            // permanente, y si el CRM ahora dice que a esa persona sí se le puede escribir hay
            // que borrar su fila y materializarla como destinataria.
            var exclusions = await _context.CampaignExclusions
                .Where(e => e.CampaignId == campaignId)
                .ToDictionaryAsync(e => e.ContactId, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var added = 0;
            var skipped = 0;
            var seen = new HashSet<Guid>();
            var excludedByReason = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var contact in contacts)
            {
                // El CRM puede devolver la misma ficha dos veces en una respuesta paginada si
                // alguien escribe entre página y página. Se cuenta una sola vez.
                if (!seen.Add(contact.Id))
                {
                    skipped++;
                    continue;
                }

                var hasEmail = !string.IsNullOrWhiteSpace(contact.Email);
                var isMailable = contact.CanBeEmailed && hasEmail;

                // Ya es destinatario: se deja como está. Que el CRM diga ahora que no se le
                // puede escribir no borra la fila, porque la recomprobación del momento de la
                // entrega es la que decide eso (spec 04 §3) y una fila borrada no deja rastro.
                if (alreadyRecipients.Contains(contact.Id))
                {
                    skipped++;
                    continue;
                }

                if (exclusions.TryGetValue(contact.Id, out var exclusion))
                {
                    // Sigue sin poder contactarse: se deja su exclusión tal cual, con el motivo
                    // con el que se escribió.
                    if (!isMailable)
                    {
                        skipped++;
                        continue;
                    }

                    // Volvió a ser contactable. Sin esto una exclusión sería para siempre: nada
                    // la quita, y esa persona no podría entrar nunca más en esta campaña.
                    _context.CampaignExclusions.Remove(exclusion);
                    exclusions.Remove(contact.Id);
                }

                if (isMailable)
                {
                    _context.CampaignRecipients.Add(new CampaignRecipientData
                    {
                        Id = Guid.CreateVersion7(),
                        CampaignId = campaignId,
                        ContactId = contact.Id,
                        FirstName = contact.FirstName,
                        LastName = contact.LastName,
                        Email = contact.Email!,
                        SendStatus = RecipientSendStatus.Pending,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });

                    added++;
                    continue;
                }

                // Un contacto contactable sin dirección no debería llegar —el CRM pliega
                // "sin correo" dentro de canBeEmailed— pero si llega se excluye por no_email
                // antes que materializar una fila a la que no se le puede escribir.
                var reason = hasEmail
                    ? ExclusionReason.FromCrm(contact.SuppressionReason)
                    : ExclusionReason.NoEmail;

                _context.CampaignExclusions.Add(new CampaignExclusionData
                {
                    Id = Guid.CreateVersion7(),
                    CampaignId = campaignId,
                    ContactId = contact.Id,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    Email = contact.Email,
                    Reason = reason,
                    ExcludedAt = now,
                });

                excludedByReason[reason] = excludedByReason.GetValueOrDefault(reason) + 1;
            }

            campaign.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);

            var excluded = excludedByReason.Values.Sum();

            return Result.Success<Maybe<RecipientSelection>, BaseError>(
                Maybe.From(new RecipientSelection(contacts.Count, added, excluded, skipped, excludedByReason)));
        });
    }

    /// <inheritdoc />
    public Task<Result<RemoveRecipientOutcome, BaseError>> RemoveRecipientAsync(
        Guid campaignId,
        Guid recipientId,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "quitar el destinatario", async () =>
        {
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);

            if (campaign is null)
                return Result.Success<RemoveRecipientOutcome, BaseError>(RemoveRecipientOutcome.CampaignNotFound);

            // Misma guarda que CampaignDraftGuard, escrita aparte porque este desenlace no es un
            // Maybe sino los tres estados de RemoveRecipientOutcome.
            if (campaign.State != CampaignState.Draft)
                return Result.Failure<RemoveRecipientOutcome, BaseError>(new CampaignNotDraftError(campaign.State));

            var recipient = await _context.CampaignRecipients
                .FirstOrDefaultAsync(r => r.Id == recipientId && r.CampaignId == campaignId, cancellationToken);

            if (recipient is null)
                return Result.Success<RemoveRecipientOutcome, BaseError>(RemoveRecipientOutcome.RecipientNotFound);

            // Se borra la fila y no se escribe una exclusión: una exclusión dice "el filtro lo
            // trajo y el CRM dijo que no", y esto es una persona que alguien quitó a mano.
            // Pintarla como exclusión mezclaría las dos cosas en la única tabla que contesta
            // "¿quién falta y por qué?".
            _context.CampaignRecipients.Remove(recipient);

            campaign.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<RemoveRecipientOutcome, BaseError>(RemoveRecipientOutcome.Removed);
        });

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Guid>, BaseError>> ListUnsyncedUnsubscribesAsync(
        CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<Guid>>(_logger, "leer las bajas sin sincronizar", async () =>
            await _context.CampaignRecipients
                .AsNoTracking()
                .Where(r => r.UnsubscribedAt != null && r.UnsubscribeSyncedAt == null)
                .Select(r => r.ContactId)
                .Distinct()
                .ToListAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> MarkUnsubscribesSyncedAsync(
        IReadOnlyList<Guid> contactIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactIds);

        if (contactIds.Count == 0)
            return UnitResult.Success<BaseError>();

        var stamped = await DbResult.TryAsync(_logger, "sellar las bajas sincronizadas", async () =>
        {
            var now = DateTimeOffset.UtcNow;

            // Se vuelve a exigir la baja y la ausencia de sello en el propio UPDATE: el CHECK de
            // la tabla no admite un sello sin baja detrás, y quien ya lo tenía conserva la hora
            // en que de verdad se sincronizó.
            return await _context.CampaignRecipients
                .Where(r => contactIds.Contains(r.ContactId)
                    && r.UnsubscribedAt != null
                    && r.UnsubscribeSyncedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.UnsubscribeSyncedAt, now)
                        .SetProperty(r => r.UpdatedAt, now),
                    cancellationToken);
        });

        // Cuántas filas se sellaron no le dice nada a quien llama —el vaciado ya sabe por quiénes
        // preguntó— así que el contador se queda acá y afuera sale solo si salió bien.
        return stamped.IsFailure ? UnitResult.Failure(stamped.Error) : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public Task<Result<Maybe<UnsubscribeTarget>, BaseError>> FindRecipientByUnsubscribeTokenAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar la baja por su token", async () =>
        {
            // Por hash, que es lo único que la columna guarda. El texto plano no llega hasta acá
            // (spec 06 §2.1). El índice ix_campaign_recipient_unsubscribe_token_hash lo hace barato.
            var row = await _context.CampaignRecipients
                .AsNoTracking()
                .Where(recipient => recipient.UnsubscribeTokenHash == tokenHash)
                .Select(recipient => new
                {
                    recipient.Id,
                    recipient.ContactId,
                    recipient.Email,
                    recipient.UnsubscribeTokenHash,
                    recipient.UnsubscribedAt,
                    recipient.UnsubscribeSyncedAt,
                })
                .FirstOrDefaultAsync(cancellationToken);

            return row is null || row.UnsubscribeTokenHash is null
                ? Maybe<UnsubscribeTarget>.None
                : Maybe.From(new UnsubscribeTarget(
                    row.Id,
                    row.ContactId,
                    row.Email,
                    row.UnsubscribeTokenHash,
                    row.UnsubscribedAt,
                    row.UnsubscribeSyncedAt));
        });

    /// <inheritdoc />
    public Task<Result<Maybe<RecipientUnsubscribe>, BaseError>> FindRecipientUnsubscribeAsync(
        Guid recipientId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar la baja del destinatario", async () =>
        {
            var row = await _context.CampaignRecipients
                .AsNoTracking()
                .Where(recipient => recipient.Id == recipientId)
                .Select(recipient => new RecipientUnsubscribe(
                    recipient.Id,
                    recipient.CampaignId,
                    recipient.ContactId,
                    recipient.UnsubscribedAt,
                    recipient.UnsubscribeSyncedAt))
                .FirstOrDefaultAsync(cancellationToken);

            return row is null ? Maybe<RecipientUnsubscribe>.None : Maybe.From(row);
        });

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> MarkUnsubscribedAsync(
        Guid recipientId,
        DateTimeOffset unsubscribedAt,
        DateTimeOffset? syncedAt,
        CancellationToken cancellationToken)
    {
        var stamped = await DbResult.TryAsync(_logger, "marcar la baja", async () =>
            // El sello de sincronización se escribe con la baja en el mismo UPDATE, nunca solo: el
            // CHECK ck_campaign_recipient_sync_requires_unsubscribe no admite lo segundo sin lo
            // primero. Un syncedAt nulo deja la fila pendiente, que el próximo lanzamiento vacía.
            await _context.CampaignRecipients
                .Where(recipient => recipient.Id == recipientId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(recipient => recipient.UnsubscribedAt, unsubscribedAt)
                        .SetProperty(recipient => recipient.UnsubscribeSyncedAt, syncedAt)
                        .SetProperty(recipient => recipient.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken));

        return stamped.IsFailure ? UnitResult.Failure(stamped.Error) : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignLaunchPreflight>, BaseError>> ReadLaunchPreflightAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer la campaña antes de lanzarla", async () =>
        {
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId)
                .Select(c => new { c.State, c.EventId, c.TemplateKey, c.Language })
                .FirstOrDefaultAsync(cancellationToken);

            if (campaign is null)
                return Maybe<CampaignLaunchPreflight>.None;

            var hasTemplate = await TemplateExistsAsync(campaign.TemplateKey, campaign.Language, cancellationToken);

            var contactIds = await _context.CampaignRecipients
                .AsNoTracking()
                .Where(r => r.CampaignId == campaignId)
                .Select(r => r.ContactId)
                .ToListAsync(cancellationToken);

            return Maybe.From(new CampaignLaunchPreflight(
                campaign.State,
                campaign.EventId,
                campaign.TemplateKey,
                campaign.Language,
                hasTemplate,
                contactIds));
        });

    /// <summary>
    /// Si la pareja <c>(clave, idioma)</c> está sembrada. Es la misma búsqueda exacta que hace el
    /// worker al armar el correo, a propósito: una pareja que pasara acá y no allá es justo la
    /// campaña atascada en <c>sending</c> que esta comprobación existe para impedir (spec
    /// <c>03</c> §4.1).
    /// </summary>
    private Task<bool> TemplateExistsAsync(string templateKey, string language, CancellationToken cancellationToken) =>
        _context.Templates
            .AsNoTracking()
            .AnyAsync(t => t.TemplateKey == templateKey && t.Language == language, cancellationToken);

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignSummary>, BaseError>> LaunchAsync(
        LaunchCampaignDto dto,
        IReadOnlyList<RecipientSuppression> suppressions,
        CampaignEventValues? eventValues,
        string? senderEmail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(suppressions);

        return DbResult.TryResultAsync(_logger, "lanzar la campaña", async () =>
        {
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == dto.Id, cancellationToken);

            if (campaign is null)
                return Result.Success<Maybe<CampaignSummary>, BaseError>(Maybe<CampaignSummary>.None);

            if (!CampaignState.CanTransition(campaign.State, CampaignState.Sending))
            {
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(
                    new CampaignTransitionNotAllowedError(campaign.State, CampaignState.Sending));
            }

            var recipients = _context.CampaignRecipients.Where(r => r.CampaignId == dto.Id);

            var recipientCount = await recipients.CountAsync(cancellationToken);

            // Los tres rechazos de §4 dejan la campaña en draft, sin sellos y sin tocar ninguna
            // fila. No hay registro de lanzamiento fallido que leer: una campaña todavía en
            // borrador ya es el registro.
            if (recipientCount == 0)
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(new CampaignHasNoRecipientsError());

            if (recipientCount != dto.ExpectedRecipientCount)
            {
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(
                    new CampaignRecipientsChangedError(dto.ExpectedRecipientCount, recipientCount));
            }

            // Contactable después de la recomprobación: la fila sigue pendiente, nadie hizo clic
            // en su enlace de baja, y el CRM no acaba de decir que no se le puede escribir. Las
            // dos comprobaciones se suman —la local nunca se quitó— y ninguna razona por su
            // cuenta sobre las bajas: quien las calcula es el CRM (spec 04 §3).
            // Un contacto está una vez en una campaña, así que la lista no debería traer
            // repetidos; agrupar igual es lo que impide que un duplicado inesperado reviente.
            var reasonByContact = suppressions
                .GroupBy(suppression => suppression.ContactId)
                .ToDictionary(group => group.Key, group => group.First().Reason);

            var suppressedIds = reasonByContact.Keys.ToList();

            var mailableCount = await recipients.CountAsync(
                r => r.SendStatus == RecipientSendStatus.Pending
                    && r.UnsubscribedAt == null
                    && !suppressedIds.Contains(r.ContactId),
                cancellationToken);

            // Se cuenta antes de tocar nada: un rechazo no deja filas suprimidas bajo una campaña
            // que se queda en borrador. Escribirlas y confiar en que nadie guarde sería confiar
            // en el contexto compartido de la petición.
            if (mailableCount == 0)
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(new CampaignHasNoMailableRecipientsError());

            var now = DateTimeOffset.UtcNow;

            await SuppressAsync(recipients, reasonByContact, suppressedIds, now, cancellationToken);

            campaign.State = CampaignState.Sending;
            campaign.LaunchedById = dto.LaunchedById;
            campaign.LaunchedAt = now;

            // La copia del evento se escribe acá y en ningún otro sitio: después del 202 no queda
            // petición en la que leerlo, y el worker no llama a events-service. Un mensaje directo
            // no trae ninguna y deja las tres columnas nulas.
            campaign.EventName = eventValues?.Name;
            campaign.EventLocation = eventValues?.Location;
            campaign.EventDates = eventValues?.Dates;
            campaign.SenderEmail = senderEmail;
            campaign.UpdatedAt = now;
            campaign.UpdatedById = dto.LaunchedById;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<Maybe<CampaignSummary>, BaseError>(
                Maybe.From(await LoadSummaryAsync(campaign.Id, cancellationToken)));
        });
    }

    /// <summary>
    /// Lleva a <c>suppressed</c> las filas que el CRM acaba de declarar no contactables, con el
    /// motivo que él mismo mandó. Solo toca las que siguen pendientes: una fila ya terminal es
    /// historia de un envío, y reescribirla la borraría.
    /// </summary>
    private static async Task SuppressAsync(
        IQueryable<CampaignRecipientData> recipients,
        IReadOnlyDictionary<Guid, string> reasonByContact,
        IReadOnlyList<Guid> suppressedIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (suppressedIds.Count == 0)
            return;

        var rows = await recipients
            .Where(r => suppressedIds.Contains(r.ContactId) && r.SendStatus == RecipientSendStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.SendStatus = RecipientSendStatus.Suppressed;
            row.FailureReason = reasonByContact[row.ContactId];
            row.UpdatedAt = now;
        }
    }

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignHandoff>, BaseError>> ReadHandoffAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer la cola de entrega de la campaña", async () =>
        {
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId)
                .Select(c => new
                {
                    c.State,
                    c.EventId,
                    c.TemplateKey,
                    c.Language,
                    c.EventName,
                    c.EventLocation,
                    c.EventDates,
                    c.Subject,
                    c.Message,
                    c.SenderEmail,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (campaign is null)
                return Maybe<CampaignHandoff>.None;

            // Solo la cola pendiente, y en orden de fila. Lo primero es lo que hace que reanudar
            // siga por donde se cortó en vez de volver a escribirle a quien ya recibió; lo segundo
            // es lo que hace que los lotes se repartan igual en dos ejecuciones.
            var pending = await _context.CampaignRecipients
                .AsNoTracking()
                .Where(r => r.CampaignId == campaignId && r.SendStatus == RecipientSendStatus.Pending)
                .OrderBy(r => r.Id)
                .Select(r => new PendingHandoffRecipient(
                    r.Id,
                    r.ContactId,
                    r.FirstName,
                    r.LastName,
                    r.Email,
                    r.RegistrationTokenHash != null || r.UnsubscribeTokenHash != null))
                .ToListAsync(cancellationToken);

            // Los tres van juntos o no van, y lo cobra una restricción de la tabla; leerlos así
            // deja que el worker distinga "sin copiar" de "copiado vacío" con una sola pregunta.
            var eventValues = campaign.EventName is null
                ? null
                : new CampaignEventValues(
                    campaign.EventName,
                    campaign.EventLocation ?? string.Empty,
                    campaign.EventDates ?? string.Empty);

            return Maybe.From(new CampaignHandoff(
                campaign.State,
                campaign.EventId,
                campaign.TemplateKey,
                campaign.Language,
                eventValues,
                pending,
                campaign.Subject,
                campaign.Message,
                campaign.SenderEmail));
        });

    /// <inheritdoc />
    public Task<Result<Maybe<string>, BaseError>> ReadStateAsync(Guid campaignId, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer el estado de la campaña", async () =>
        {
            var state = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId)
                .Select(c => c.State)
                .FirstOrDefaultAsync(cancellationToken);

            return state is null ? Maybe<string>.None : Maybe.From(state);
        });

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> RecordHandoffAsync(
        Guid campaignId,
        IReadOnlyList<RecipientHandoffOutcome> outcomes,
        IReadOnlyList<RecipientTokenHashes> tokenHashes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(tokenHashes);

        if (outcomes.Count == 0)
            return UnitResult.Success<BaseError>();

        var recorded = await DbResult.TryAsync(_logger, "anotar la entrega del lote", async () =>
        {
            var hashesByRecipient = tokenHashes
                .GroupBy(hashes => hashes.RecipientId)
                .ToDictionary(group => group.Key, group => group.First());

            // El transporte no debería contestar dos veces por la misma fila; agrupar igual es lo
            // que impide que un duplicado inesperado reviente.
            var byRecipient = outcomes
                .GroupBy(outcome => outcome.RecipientId)
                .ToDictionary(group => group.Key, group => group.First());

            var recipientIds = byRecipient.Keys.ToList();

            // La guarda es `pending`: una fila ya terminal es historia de una entrega y
            // reescribirla la borraría. Es también lo que hace inofensivo anotar dos veces el
            // mismo lote.
            var rows = await _context.CampaignRecipients
                .Where(r => r.CampaignId == campaignId
                    && recipientIds.Contains(r.Id)
                    && r.SendStatus == RecipientSendStatus.Pending)
                .ToListAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;

            foreach (var row in rows)
            {
                var outcome = byRecipient[row.Id];

                row.SendStatus = outcome.SendStatus;
                row.FailureReason = outcome.FailureReason;
                row.NotificationId = outcome.NotificationId;
                row.UpdatedAt = now;

                // El hash solo se guarda si el correo salió. Una dirección que el transporte
                // rechazó no lleva ningún enlace encima, así que su credencial sería peso muerto.
                // Y nunca se pisa uno ya puesto: sería romper un enlace que ya está en la bandeja
                // de entrada de alguien.
                if (outcome.SendStatus != RecipientSendStatus.HandedOff
                    || !hashesByRecipient.TryGetValue(row.Id, out var hashes)
                    || row.RegistrationTokenHash is not null
                    || row.UnsubscribeTokenHash is not null)
                {
                    continue;
                }

                row.RegistrationTokenHash = hashes.RegistrationTokenHash;
                row.UnsubscribeTokenHash = hashes.UnsubscribeTokenHash;
            }

            return await _context.SaveChangesAsync(cancellationToken);
        });

        return recorded.IsFailure ? UnitResult.Failure(recorded.Error) : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> CompleteHandoffAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var completed = await DbResult.TryAsync(_logger, "cerrar la entrega de la campaña", async () =>
        {
            var now = DateTimeOffset.UtcNow;

            // Las dos condiciones van dentro del UPDATE y no en una lectura previa. Leer y decidir
            // después dejaría que una cancelación que entre en medio acabara pisada por `sent`, y
            // acá no hay nadie mirando un código de estado que lo delate: el worker corre solo.
            //
            // Las dos casillas que cierran son las mismas que entregan, ver
            // CampaignHandoff.IsDeliverable: `sending` es el salto que admite la tabla de
            // transiciones (ver CampaignState), y `sent` no es un salto en absoluto —es volver a
            // cerrar lo que `retry-failed` reabrió, con `sent_at` puesto al día. Se escribe como
            // predicado porque es lo que la base sabe comprobar a la vez que escribe.
            var flipped = await _context.Campaigns
                .Where(c => c.Id == campaignId
                    && (c.State == CampaignState.Sending || c.State == CampaignState.Sent)
                    && !_context.CampaignRecipients.Any(r =>
                        r.CampaignId == campaignId && r.SendStatus == RecipientSendStatus.Pending))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.State, CampaignState.Sent)
                        .SetProperty(c => c.SentAt, now)
                        .SetProperty(c => c.UpdatedAt, now),
                    cancellationToken);

            // `updated_by` se queda en quien lanzó: acá no hay persona detrás, y poner un id de
            // servicio en una columna que responde "quién fue" la volvería mentira.
            return flipped == 1;
        });

        if (completed.IsFailure)
            return UnitResult.Failure(completed.Error);

        // Los dos desenlaces se dicen, porque los dos son noticias. El que no salta es el que
        // acaba en `strandedCampaigns` (spec 07 §6) cuando esa pantalla exista: hasta entonces
        // esta línea es lo único que cuenta que una campaña se quedó a medias.
        if (completed.Value)
            _logger.LogInformation("La campaña {CampaignId} quedó entregada por completo.", campaignId);
        else
            _logger.LogInformation(
                "La campaña {CampaignId} sigue sin cerrarse: le quedan filas pendientes o ya no admite el salto.",
                campaignId);

        return UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignSummary>, BaseError>> ResumeHandoffAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "reanudar la entrega de la campaña", async () =>
        {
            // No se escribe nada. La cola sin entregar ya es lo que está en `pending`, así que
            // reanudar es volver a encolar el id y nada más: es lo que hace que reanudar no
            // pueda escribirle dos veces a quien ya recibió (spec 04 §5).
            var state = await ReadStateOrNoneAsync(campaignId, cancellationToken);

            if (state.HasNoValue)
                return Result.Success<Maybe<CampaignSummary>, BaseError>(Maybe<CampaignSummary>.None);

            if (state.Value != CampaignState.Sending)
            {
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(
                    new CampaignNotSendingError(state.Value));
            }

            return Result.Success<Maybe<CampaignSummary>, BaseError>(
                Maybe.From(await LoadSummaryAsync(campaignId, cancellationToken)));
        });

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignSummary>, BaseError>> RetryFailedRecipientsAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "reintentar los destinatarios rechazados", async () =>
        {
            var state = await ReadStateOrNoneAsync(campaignId, cancellationToken);

            if (state.HasNoValue)
                return Result.Success<Maybe<CampaignSummary>, BaseError>(Maybe<CampaignSummary>.None);

            // `sent` también, y es el caso normal: una campaña con filas rechazadas ya está
            // cerrada, porque lo que la cierra es que no le quede ninguna pendiente y eso pasa con
            // la primera vuelta. No vuelve atrás —`sent` sigue siendo terminal y la tabla de la
            // spec 03 §2 no gana ninguna arista—, se le reabre una cola y el worker la cierra otra
            // vez. `draft` y `cancelled` no: una nunca salió y la otra la paró alguien.
            if (state.Value is not (CampaignState.Sending or CampaignState.Sent))
            {
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(
                    new CampaignNotSendingError(state.Value));
            }

            var now = DateTimeOffset.UtcNow;

            // La guarda de estado viaja dentro del propio UPDATE, como en CompleteHandoffAsync:
            // una cancelación que entre entre la lectura de arriba y esta línea gana, en vez de
            // devolver a la cola filas de una campaña que alguien acaba de parar.
            var reopened = await _context.CampaignRecipients
                .Where(r => r.CampaignId == campaignId
                    && r.SendStatus == RecipientSendStatus.Failed
                    && _context.Campaigns.Any(c => c.Id == campaignId
                        && (c.State == CampaignState.Sending || c.State == CampaignState.Sent)))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.SendStatus, RecipientSendStatus.Pending)
                        .SetProperty(r => r.FailureReason, (string?)null)
                        .SetProperty(r => r.UpdatedAt, now),
                    cancellationToken);

            // Cero no es un fallo: es que no había ninguna rechazada, y volver a encolar la
            // campaña con la cola que tenga es inofensivo. Se dice porque es la diferencia entre
            // "se reintentaron doce" y "no había nada que reintentar", que es justo lo que quien
            // apretó el botón quiere saber.
            _logger.LogInformation(
                "La campaña {CampaignId} devolvió {Count} destinatarios rechazados a la cola.",
                campaignId,
                reopened);

            return Result.Success<Maybe<CampaignSummary>, BaseError>(
                Maybe.From(await LoadSummaryAsync(campaignId, cancellationToken)));
        });

    /// <summary>El estado de una campaña, o nada si ese id no existe.</summary>
    private async Task<Maybe<string>> ReadStateOrNoneAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var state = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => c.State)
            .FirstOrDefaultAsync(cancellationToken);

        return state is null ? Maybe<string>.None : Maybe.From(state);
    }

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignSummary>, BaseError>> CancelAsync(
        Guid id,
        Guid cancelledById,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "cancelar la campaña", async () =>
        {
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (campaign is null)
                return Result.Success<Maybe<CampaignSummary>, BaseError>(Maybe<CampaignSummary>.None);

            if (!CampaignState.CanTransition(campaign.State, CampaignState.Cancelled))
            {
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(
                    new CampaignTransitionNotAllowedError(campaign.State, CampaignState.Cancelled));
            }

            // Ninguna fila de destinatario se toca. Las que quedaron pendientes se quedan
            // pendientes: una fila pending bajo una campaña cancelada se lee correctamente como
            // "pretendíamos escribirle y paramos", y reescribirlas borraría esa lectura.
            campaign.State = CampaignState.Cancelled;

            // La carga sin confirmar sí se va, con sus filas detrás por la cascada. Es lo que
            // sostiene que las cargas no necesiten ni caducidad ni tarea de limpieza: un
            // borrador abandonado se lleva su lote al cancelarse (spec 05 §2). Las cargas ya
            // confirmadas se quedan, porque son permanentes por otra razón —registran quién
            // confirmó, que es lo que hace aceptable el aplazamiento de `onBehalfOfUserId`— y
            // porque sus contactos existen en el CRM se cancele esto o no.
            var pendingUploads = await _context.CampaignUploadBatches
                .Where(b => b.CampaignId == id && b.ConfirmedAt == null)
                .ToListAsync(cancellationToken);

            _context.CampaignUploadBatches.RemoveRange(pendingUploads);

            campaign.UpdatedAt = DateTimeOffset.UtcNow;
            campaign.UpdatedById = cancelledById;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<Maybe<CampaignSummary>, BaseError>(
                Maybe.From(await LoadSummaryAsync(campaign.Id, cancellationToken)));
        });

    /// <summary>
    /// Relee la campaña con sus dos cuentas después de una escritura. Se relee en vez de
    /// contar en memoria porque las cuentas son sobre otras dos tablas, y la respuesta tiene
    /// que decir lo que hay en la base y no lo que esta petición creyó dejar.
    /// </summary>
    private async Task<CampaignSummary> LoadSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var projection = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CampaignProjection(c, c.Recipients.Count, c.Exclusions.Count))
            .FirstAsync(cancellationToken);

        return ToSummary(projection);
    }

    private static CampaignSummary ToSummary(CampaignProjection projection) =>
        new(
            new Campaign(
                projection.Campaign.Id,
                projection.Campaign.EventId,
                projection.Campaign.TemplateKey,
                projection.Campaign.Language,
                projection.Campaign.State,
                projection.Campaign.LaunchedById,
                projection.Campaign.LaunchedAt,
                projection.Campaign.SentAt,
                projection.Campaign.CreatedAt,
                projection.Campaign.UpdatedAt,
                projection.Campaign.CreatedById,
                projection.Campaign.UpdatedById,
                projection.Campaign.Subject,
                projection.Campaign.Message),
            projection.RecipientCount,
            projection.ExclusionCount);

    /// <summary>
    /// La fila más sus dos cuentas, en una sola consulta. Es un tipo interno y no el
    /// <see cref="CampaignSummary"/> del dominio porque ese lleva la entidad con sus guardas, y
    /// EF no puede construir una entidad con constructor validador dentro de un <c>Select</c>.
    /// </summary>
    private sealed record CampaignProjection(CampaignData Campaign, int RecipientCount, int ExclusionCount);
}

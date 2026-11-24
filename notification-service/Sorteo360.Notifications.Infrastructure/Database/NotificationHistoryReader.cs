using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Notifications.Core.Database;
using Sorteo360.Notifications.Core.DTOs.History;
using Sorteo360.Notifications.Core.History;
using Sorteo360.Notifications.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <inheritdoc cref="INotificationHistoryReader" />
public sealed class NotificationHistoryReader : INotificationHistoryReader
{
    /// <summary>
    /// Los estados que solo escribe el webhook, como arreglo: es la forma que EF traduce a un
    /// <c>IN (...)</c> dentro del <c>CASE</c> que aplica el tope en SQL.
    /// </summary>
    private static readonly string[] ProviderDerived = [.. DeliveryTracking.ProviderDerived];

    private readonly NotificationsDbContext _context;
    private readonly ILogger<NotificationHistoryReader> _logger;

    public NotificationHistoryReader(NotificationsDbContext context, ILogger<NotificationHistoryReader> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<HistoryRecipientDto>, BaseError>> ListRecipientsAsync(
        NotificationHistoryQueryDto query,
        HistoryScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);

        return DbResult.TryAsync<PagedResult<HistoryRecipientDto>>(_logger, "listar el historial de envíos", async () =>
        {
            var rows = MatchingRows(query, scope);

            var total = await rows.CountAsync(cancellationToken);

            // Orden estable: la fecha decide, y el id desempata para que dos filas de la misma
            // milésima no se turnen entre páginas.
            var page = await rows
                .OrderByDescending(row => row.Recipient.QueuedAt)
                .ThenBy(row => row.Recipient.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(row => new RecipientReadModel(
                    row.Recipient.Id,
                    row.Recipient.BatchId,
                    row.Batch.Type,
                    row.Batch.Subject,
                    row.Batch.Language,
                    row.Batch.FromAddress,
                    row.Batch.FromName,
                    row.Recipient.Email,
                    row.Recipient.Name,
                    row.Recipient.DeliveryStatus,
                    row.Recipient.RecipientAction,
                    row.Batch.TrackingExpected,
                    row.Recipient.QueuedAt,
                    row.Recipient.SentAt,
                    row.Recipient.AttemptCount,
                    row.Recipient.LastError,
                    row.Recipient.SgMessageId,
                    row.Recipient.SmtpQueuedAs))
                .ToListAsync(cancellationToken);

            var about = await AboutByBatchAsync(
                [.. page.Select(row => row.BatchId).Distinct()], scope, cancellationToken);

            var items = page.Select(row => Map(row, about)).ToList();

            return new PagedResult<HistoryRecipientDto>(items, total, query.Page, query.PageSize);
        });
    }

    /// <inheritdoc />
    public Task<Result<Maybe<HistoryBatchDto>, BaseError>> FindBatchAsync(
        Guid batchId,
        HistoryScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return DbResult.TryAsync<Maybe<HistoryBatchDto>>(_logger, "obtener el envío del historial", async () =>
        {
            var batches = _context.NotificationBatches.AsNoTracking().Where(batch => batch.Id == batchId);

            batches = WithinScope(batches, scope);

            var batch = await batches
                .Select(b => new BatchReadModel(
                    b.Id, b.Type, b.Subject, b.Body, b.Language,
                    b.FromAddress, b.FromName, b.TrackingExpected, b.BodySha256, b.CreatedAt))
                .SingleOrDefaultAsync(cancellationToken);

            if (batch is null)
                return Maybe<HistoryBatchDto>.None;

            var about = await AboutByBatchAsync([batchId], scope, cancellationToken);

            var recipients = await _context.NotificationRecipients
                .AsNoTracking()
                .Where(recipient => recipient.BatchId == batchId)
                .OrderBy(recipient => recipient.QueuedAt)
                .ThenBy(recipient => recipient.Id)
                .Select(recipient => new RecipientReadModel(
                    recipient.Id,
                    recipient.BatchId,
                    batch.Type,
                    batch.Subject,
                    batch.Language,
                    batch.FromAddress,
                    batch.FromName,
                    recipient.Email,
                    recipient.Name,
                    recipient.DeliveryStatus,
                    recipient.RecipientAction,
                    batch.TrackingExpected,
                    recipient.QueuedAt,
                    recipient.SentAt,
                    recipient.AttemptCount,
                    recipient.LastError,
                    recipient.SgMessageId,
                    recipient.SmtpQueuedAs))
                .ToListAsync(cancellationToken);

            return Maybe<HistoryBatchDto>.From(new HistoryBatchDto(
                batch.Id,
                batch.Type,
                batch.Subject,
                batch.Body,
                batch.Language,
                batch.FromAddress,
                batch.FromName,
                batch.TrackingExpected,
                batch.BodySha256,
                batch.CreatedAt,
                about.TryGetValue(batchId, out var entries) ? entries : [],
                [.. recipients.Select(recipient => Map(recipient, about))]));
        });
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<OrphanEventDto>, BaseError>> ListOrphanEventsAsync(
        int page,
        int pageSize,
        HistoryScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return DbResult.TryAsync<PagedResult<OrphanEventDto>>(_logger, "listar los eventos huérfanos", async () =>
        {
            // Un huérfano no tiene envío, así que no hay source_service contra el que acotarlo. Se
            // falla cerrado: una cuenta de servicio ve una lista vacía en vez de los rebotes de otro.
            if (scope.IsScoped)
                return new PagedResult<OrphanEventDto>([], 0, page, pageSize);

            var orphans = _context.NotificationEvents
                .AsNoTracking()
                .Where(notificationEvent => notificationEvent.RecipientId == null);

            var total = await orphans.CountAsync(cancellationToken);

            var items = await orphans
                .OrderByDescending(notificationEvent => notificationEvent.OccurredAt)
                .ThenBy(notificationEvent => notificationEvent.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(notificationEvent => new OrphanEventDto(
                    notificationEvent.Id,
                    notificationEvent.SgEventId,
                    notificationEvent.EventType,
                    notificationEvent.OccurredAt,
                    notificationEvent.Payload))
                .ToListAsync(cancellationToken);

            return new PagedResult<OrphanEventDto>(items, total, page, pageSize);
        });
    }

    /// <summary>
    /// La consulta base del listado con todos los filtros aplicados, sin ordenar ni paginar: la
    /// usan tanto el conteo total como la página, así que las dos cuentan lo mismo.
    /// </summary>
    private IQueryable<RecipientWithBatch> MatchingRows(NotificationHistoryQueryDto query, HistoryScope scope)
    {
        var rows =
            from recipient in _context.NotificationRecipients.AsNoTracking()
            join batch in _context.NotificationBatches.AsNoTracking() on recipient.BatchId equals batch.Id
            select new RecipientWithBatch { Recipient = recipient, Batch = batch };

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            rows = rows.Where(row => row.Batch.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.DeliveryStatus))
        {
            // Se compara contra el estado reportado, no contra la columna: si el tope devuelve
            // una fila a 'sent', filtrar por 'delivered' no puede traerla y luego mostrarla como
            // 'sent'. Lo que se filtra es lo que se ve.
            //
            // El tope está escrito dos veces a la fuerza: acá en SQL, porque el filtro corre en la
            // base, y en DeliveryTracking.Reported para la proyección. Las dos tienen que cambiar
            // juntas, y si se separan falla el test de filtrar por 'delivered' una fila sin
            // seguimiento.
            var status = query.DeliveryStatus.Trim();
            rows = rows.Where(row =>
                (!row.Batch.TrackingExpected && ProviderDerived.Contains(row.Recipient.DeliveryStatus)
                    ? Core.Notifications.DeliveryStatus.Sent
                    : row.Recipient.DeliveryStatus)
                == status);
        }

        if (!string.IsNullOrWhiteSpace(query.RecipientAction))
        {
            var action = query.RecipientAction.Trim();
            rows = rows.Where(row => row.Recipient.RecipientAction == action);
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            // Coincidencia exacta, no un LIKE ni un plegado de mayúsculas: la dirección se guarda
            // tal como la escribió quien llamó, y buscar por parecido es machinery de búsqueda que
            // nadie ha pedido todavía.
            var email = query.Email.Trim();
            rows = rows.Where(row => row.Recipient.Email == email);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType) && !string.IsNullOrWhiteSpace(query.EntityId))
        {
            var entityType = query.EntityType.Trim();
            var entityId = query.EntityId.Trim();

            // Cae en el índice (entity_type, entity_id): es la consulta que el cliente hace de
            // verdad, "todo lo que se mandó sobre este evento", y trae las filas de todos los
            // envíos que lo mencionan y no las de uno.
            rows = rows.Where(row => _context.NotificationAbout.Any(about =>
                about.BatchId == row.Batch.Id
                && about.EntityType == entityType
                && about.EntityId == entityId));

        }

        if (!string.IsNullOrWhiteSpace(query.SourceService))
        {
            var sourceService = query.SourceService.Trim();
            rows = rows.Where(row => _context.NotificationAbout.Any(about =>
                about.BatchId == row.Batch.Id && about.SourceService == sourceService));
        }

        if (query.Ids is { Count: > 0 })
        {
            var ids = query.Ids.ToArray();
            rows = rows.Where(row => ids.Contains(row.Recipient.Id));
        }

        if (query.SentFrom is not null)
            rows = rows.Where(row => row.Recipient.QueuedAt >= query.SentFrom.Value);

        if (query.SentTo is not null)
            rows = rows.Where(row => row.Recipient.QueuedAt <= query.SentTo.Value);

        // El mismo "trata de algo de este servicio" que el filtro de arriba, escrito otra vez en
        // lugar de compartir un método: los tres sitios que lo preguntan corren dentro de un árbol
        // de expresión, y una llamada a un método propio no se traduce a SQL —se ejecutaría en
        // memoria sobre la tabla entera, si es que llega a ejecutarse.
        if (scope.SourceService is { } callerService)
        {
            rows = rows.Where(row => _context.NotificationAbout.Any(about =>
                about.BatchId == row.Batch.Id && about.SourceService == callerService));
        }

        return rows;
    }

    /// <summary>Acota una consulta de envíos al servicio de quien llama, si viene acotado.</summary>
    private IQueryable<NotificationBatchData> WithinScope(IQueryable<NotificationBatchData> batches, HistoryScope scope)
    {
        if (scope.SourceService is not { } callerService)
            return batches;

        return batches.Where(batch => _context.NotificationAbout.Any(about =>
            about.BatchId == batch.Id && about.SourceService == callerService));
    }

    /// <summary>
    /// Las entidades de cada uno de esos envíos, en una sola consulta. Una por fila de persona
    /// sería una consulta por fila de la página.
    /// <para>
    /// A quien llama acotado se le devuelven solo sus propias entidades. Un envío puede tratar de
    /// cosas de más de un servicio, y entonces alcanzarlo no es alcanzar los ids de los demás.
    /// </para>
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<HistoryAboutDto>>> AboutByBatchAsync(
        Guid[] batchIds,
        HistoryScope scope,
        CancellationToken cancellationToken)
    {
        if (batchIds.Length == 0)
            return [];

        var about = _context.NotificationAbout
            .AsNoTracking()
            .Where(about => batchIds.Contains(about.BatchId));

        if (scope.SourceService is { } callerService)
            about = about.Where(entry => entry.SourceService == callerService);

        var entries = await about
            .OrderBy(about => about.EntityType)
            .ThenBy(about => about.EntityId)
            .Select(about => new
            {
                about.BatchId,
                Dto = new HistoryAboutDto(about.SourceService, about.EntityType, about.EntityId)
            })
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(entry => entry.BatchId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<HistoryAboutDto>)[.. group.Select(entry => entry.Dto)]);
    }

    /// <summary>
    /// Pasa la proyección de persistencia al contrato, aplicando el tope del seguimiento. El tope
    /// se aplica acá y no en la columna: la columna guarda lo que pasó.
    /// </summary>
    private static HistoryRecipientDto Map(
        RecipientReadModel recipient,
        Dictionary<Guid, IReadOnlyList<HistoryAboutDto>> about) =>
        new(
            recipient.Id,
            recipient.BatchId,
            recipient.Type,
            recipient.Subject,
            recipient.Language,
            recipient.FromAddress,
            recipient.FromName,
            recipient.Email,
            recipient.Name,
            DeliveryTracking.Reported(recipient.DeliveryStatus, recipient.TrackingExpected),
            recipient.RecipientAction,
            recipient.TrackingExpected,
            recipient.QueuedAt,
            recipient.SentAt,
            recipient.AttemptCount,
            recipient.LastError,
            recipient.SgMessageId,
            recipient.SmtpQueuedAs,
            about.TryGetValue(recipient.BatchId, out var entries) ? entries : []);

    /// <summary>La fila de persona con su envío al lado, que es la unidad que el listado filtra.</summary>
    private sealed class RecipientWithBatch
    {
        public required NotificationRecipientData Recipient { get; init; }
        public required NotificationBatchData Batch { get; init; }
    }

    private sealed record RecipientReadModel(
        Guid Id,
        Guid BatchId,
        string Type,
        string Subject,
        string Language,
        string FromAddress,
        string? FromName,
        string Email,
        string? Name,
        string DeliveryStatus,
        string RecipientAction,
        bool TrackingExpected,
        DateTimeOffset QueuedAt,
        DateTimeOffset? SentAt,
        int AttemptCount,
        string? LastError,
        string? SgMessageId,
        string? SmtpQueuedAs);

    private sealed record BatchReadModel(
        Guid Id,
        string Type,
        string Subject,
        string Body,
        string Language,
        string FromAddress,
        string? FromName,
        bool TrackingExpected,
        string BodySha256,
        DateTimeOffset CreatedAt);
}

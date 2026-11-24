using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Notifications.Core.Database;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Infrastructure.Database.Configurations;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;
using Npgsql;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <inheritdoc cref="INotificationBatchRepository" />
public sealed class NotificationBatchRepository : INotificationBatchRepository
{
    /// <summary>SQLSTATE de violación de unicidad en PostgreSQL.</summary>
    private const string UniqueViolation = "23505";

    private readonly NotificationsDbContext _context;
    private readonly ILogger<NotificationBatchRepository> _logger;

    public NotificationBatchRepository(NotificationsDbContext context, ILogger<NotificationBatchRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<StoredBatch, BaseError>> AddAsync(
        AcceptedSend send,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);

        var (batch, about, recipients, payloads) = send;

        return DbResult.TryResultAsync(_logger, "guardar el envío de notificación", async () =>
        {
            // Camino rápido: la clave ya existe y no hay nada que escribir. Es lo normal cuando
            // quien llama reintenta, y le ahorra a la base una violación de índice.
            var existing = await FindByIdempotencyKeyAsync(batch.IdempotencyKey, cancellationToken);

            if (existing is not null)
                return Result.Success<StoredBatch, BaseError>(existing);

            _context.NotificationBatches.Add(ToData(batch));
            _context.NotificationAbout.AddRange(about.Select(ToData));
            _context.NotificationRecipients.AddRange(recipients.Select(ToData));

            // Los valores van en la misma transacción que la fila que explican: si el envío existe,
            // lo que hace falta para mandarlo existe con él.
            _context.NotificationPayloads.AddRange(payloads.Select(payload => ToData(payload, batch.CreatedAt)));

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
            {
                // Otra petición idéntica llegó primero. Se lee lo que escribió y se contesta con
                // sus ids: la carrera la decide el índice, no un candado.
                _logger.LogInformation(
                    "La clave de idempotencia {IdempotencyKey} la ganó otra petición; se replican sus ids",
                    batch.IdempotencyKey);

                _context.ChangeTracker.Clear();

                var winner = await FindByIdempotencyKeyAsync(batch.IdempotencyKey, cancellationToken);

                if (winner is null)
                    throw;

                return Result.Success<StoredBatch, BaseError>(winner);
            }

            var stored = recipients
                .Select(recipient => new StoredRecipient(recipient.Id, recipient.Email))
                .ToArray();

            return Result.Success<StoredBatch, BaseError>(
                new StoredBatch(batch.Id, batch.BodySha256, stored));
        });
    }

    /// <summary>
    /// Lee el envío que ya ocupa la clave, con sus filas de persona. Devuelve null si la clave está
    /// libre.
    /// </summary>
    private async Task<StoredBatch?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var batch = await _context.NotificationBatches
            .AsNoTracking()
            .Where(b => b.IdempotencyKey == idempotencyKey)
            .Select(b => new { b.Id, b.BodySha256 })
            .SingleOrDefaultAsync(cancellationToken);

        if (batch is null)
            return null;

        var recipients = await _context.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.BatchId == batch.Id)
            .OrderBy(r => r.QueuedAt)
            .ThenBy(r => r.Id)
            .Select(r => new StoredRecipient(r.Id, r.Email))
            .ToArrayAsync(cancellationToken);

        return new StoredBatch(batch.Id, batch.BodySha256, recipients);
    }

    /// <summary>
    /// Si el fallo es exactamente la colisión de la clave de idempotencia. Se mira el nombre del
    /// índice y no solo el SQLSTATE: cualquier otra violación de unicidad es un problema distinto y
    /// no se puede contestar replicando un envío.
    /// </summary>
    private static bool IsIdempotencyKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres
        && string.Equals(
            postgres.ConstraintName,
            NotificationBatchConfiguration.IdempotencyKeyIndex,
            StringComparison.Ordinal);

    private static NotificationBatchData ToData(NotificationBatch batch) => new()
    {
        Id = batch.Id,
        Type = batch.Type,
        FromAddress = batch.FromAddress,
        FromName = batch.FromName,
        Subject = batch.Subject,
        Body = batch.Body,
        Language = batch.Language,
        IdempotencyKey = batch.IdempotencyKey,
        BodySha256 = batch.BodySha256,
        TrackingExpected = batch.TrackingExpected,
        TraceId = batch.TraceId,
        CreatedAt = batch.CreatedAt
    };

    private static NotificationAboutData ToData(NotificationAbout about) => new()
    {
        Id = about.Id,
        BatchId = about.BatchId,
        SourceService = about.SourceService,
        EntityType = about.EntityType,
        EntityId = about.EntityId
    };

    private static NotificationPayloadData ToData(NotificationPayload payload, DateTimeOffset createdAt) => new()
    {
        RecipientId = payload.RecipientId,
        RenderValues = JsonSerializer.Serialize(payload.Values, NotificationPayloadData.Json),
        CreatedAt = createdAt
    };

    private static NotificationRecipientData ToData(NotificationRecipient recipient) => new()
    {
        Id = recipient.Id,
        BatchId = recipient.BatchId,
        Email = recipient.Email,
        Name = recipient.Name,
        DeliveryStatus = recipient.DeliveryStatus,
        RecipientAction = recipient.RecipientAction,
        AttemptCount = recipient.AttemptCount,
        NextAttemptAt = recipient.NextAttemptAt,
        QueuedAt = recipient.QueuedAt,
        SentAt = recipient.SentAt,
        LastError = recipient.LastError,
        SgMessageId = recipient.SgMessageId,
        SmtpQueuedAs = recipient.SmtpQueuedAs
    };
}

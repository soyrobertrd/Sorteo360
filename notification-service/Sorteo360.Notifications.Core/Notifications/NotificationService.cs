using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Sorteo360.Notifications.Core.Database;
using Sorteo360.Notifications.Core.DTOs;
using Sorteo360.Notifications.Core.Errors;
using Sorteo360.Notifications.Core.Rendering;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Notifications;

/// <inheritdoc cref="INotificationService" />
public sealed class NotificationService : INotificationService
{
    private readonly INotificationBatchRepository _repository;
    private readonly IOptions<SendGridEventWebhookSettings> _eventWebhook;

    public NotificationService(
        INotificationBatchRepository repository,
        IOptions<SendGridEventWebhookSettings> eventWebhook)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventWebhook);

        _repository = repository;
        _eventWebhook = eventWebhook;
    }

    /// <inheritdoc />
    public async Task<Result<AcceptedNotification, BaseError>> AcceptAsync(
        SendNotificationDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var checks = Check(dto);

        if (checks.IsFailure)
            return Result.Failure<AcceptedNotification, BaseError>(checks.Error);

        var now = DateTimeOffset.UtcNow;
        var batchId = Guid.NewGuid();
        var fingerprint = NotificationFingerprint.Compute(dto);

        var batch = new NotificationBatch(
            id: batchId,
            type: dto.Type,
            language: dto.Language,
            fromAddress: dto.FromAddress,
            fromName: dto.FromName,
            subject: dto.Subject,
            body: dto.Body,
            idempotencyKey: dto.IdempotencyKey,
            bodySha256: fingerprint,
            // Se estampa acá, con lo que la configuración dice hoy, y la fila no vuelve a
            // consultarla: encender el webhook mañana no reescribe la historia de ayer.
            trackingExpected: _eventWebhook.Value.Enabled,
            traceId: dto.TraceId,
            createdAt: now);

        var about = dto.About
            .Select(entry => new NotificationAbout(
                id: Guid.NewGuid(),
                batchId: batchId,
                sourceService: entry.SourceService,
                entityType: entry.EntityType,
                entityId: entry.EntityId))
            .ToArray();

        // Una dirección que no es una dirección es lo único que se rechaza por persona, y se
        // rechaza sin fila: no hay nada que intentar ni nada que el historial pueda contar de ella.
        var accepted = new List<NotificationRecipient>();
        var payloads = new List<NotificationPayload>();
        var pending = new List<(string Email, NotificationRecipient? Row)>();

        foreach (var recipient in dto.Recipients)
        {
            if (!EmailAddressSyntax.IsValid(recipient.Email))
            {
                pending.Add((recipient.Email, null));
                continue;
            }

            var row = NotificationRecipient.Queued(batchId, recipient.Email.Trim(), recipient.Name, now);
            accepted.Add(row);
            payloads.Add(NotificationPayload.For(row.Id, dto.SharedData, recipient.Data));
            pending.Add((row.Email, row));
        }

        var stored = await _repository.AddAsync(
            new AcceptedSend(batch, about, accepted, payloads),
            cancellationToken);

        if (stored.IsFailure)
            return Result.Failure<AcceptedNotification, BaseError>(stored.Error);

        // La clave ya existía: no se escribió nada y lo que vuelve es el envío de antes.
        if (stored.Value.BatchId != batchId)
            return Replay(dto, stored.Value);

        var results = pending
            .Select(entry => entry.Row is null
                ? RecipientOutcome.ForInvalidAddress(entry.Email)
                : RecipientOutcome.ForAccepted(entry.Row.Id, entry.Email))
            .ToArray();

        return Result.Success<AcceptedNotification, BaseError>(new AcceptedNotification(batchId, results));
    }

    /// <summary>
    /// Comprobaciones que rechazan la petición entera, en el orden en que las declara el contrato.
    /// Ninguna toca la base de datos: antes del 202 no existe ni una fila.
    /// </summary>
    private static UnitResult<BaseError> Check(SendNotificationDto dto)
    {
        if (dto.Recipients.Count > NotificationErrors.RecipientCap)
            return UnitResult.Failure(NotificationErrors.RecipientCapExceeded(dto.Recipients.Count));

        foreach (var recipient in dto.Recipients)
        {
            foreach (var key in recipient.Data.Keys)
            {
                if (dto.SharedData.ContainsKey(key))
                    return UnitResult.Failure<BaseError>(new DataKeyCollisionError(key, recipient.Email));
            }
        }

        // Las claves salen del propio cuerpo de quien llama: el servicio no tiene lista de claves
        // legales contra la que comparar, solo pregunta si cada una tiene valor. Se comprueba
        // persona por persona porque un valor puede faltar para una sola de varias.
        var placeholders = PlaceholderScanner.Scan(dto.Subject, dto.Body);

        foreach (var recipient in dto.Recipients)
        {
            foreach (var placeholder in placeholders)
            {
                if (!dto.SharedData.ContainsKey(placeholder) && !recipient.Data.ContainsKey(placeholder))
                    return UnitResult.Failure<BaseError>(new PlaceholderUnresolvedError(placeholder, recipient.Email));
            }
        }

        return UnitResult.Success<BaseError>();
    }

    /// <summary>
    /// Contesta la repetición de una clave de idempotencia. Con el mismo contenido replica el envío
    /// que ya existe —los mismos ids— porque replicar es simplemente verdad: ese envío está ahí. Un
    /// 409 acá sería una falsa alarma sobre una petición que funcionó, y es justo lo que hace seguro
    /// que el transporte reintente un POST a ciegas.
    /// <para>
    /// Con otro contenido no hay nada que replicar y el conflicto se dice en voz alta.
    /// </para>
    /// </summary>
    private static Result<AcceptedNotification, BaseError> Replay(SendNotificationDto dto, StoredBatch stored)
    {
        var fingerprint = NotificationFingerprint.Compute(dto);

        if (!string.Equals(stored.BodySha256, fingerprint, StringComparison.Ordinal))
            return Result.Failure<AcceptedNotification, BaseError>(
                NotificationErrors.IdempotencyKeyReused(dto.IdempotencyKey));

        // El contenido es el mismo, así que las direcciones son las mismas: se recorre la petición
        // en orden y cada una toma el id de su fila. Dos personas con la misma dirección tienen dos
        // filas idénticas, y da igual cuál de las dos se le asigne a cada una.
        var byEmail = stored.Recipients
            .GroupBy(recipient => recipient.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Queue<Guid>(group.Select(recipient => recipient.Id)),
                StringComparer.OrdinalIgnoreCase);

        var results = new List<RecipientOutcome>(dto.Recipients.Count);

        foreach (var recipient in dto.Recipients)
        {
            if (byEmail.TryGetValue(recipient.Email.Trim(), out var ids) && ids.Count > 0)
            {
                results.Add(RecipientOutcome.ForAccepted(ids.Dequeue(), recipient.Email));
                continue;
            }

            results.Add(RecipientOutcome.ForInvalidAddress(recipient.Email));
        }

        return Result.Success<AcceptedNotification, BaseError>(
            new AcceptedNotification(stored.BatchId, results));
    }
}

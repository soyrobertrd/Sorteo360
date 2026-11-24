using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Sorteo360.Notifications.Core.Webhooks;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;
using Npgsql;
using NpgsqlTypes;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <inheritdoc cref="INotificationEventIngest" />
public sealed class NotificationEventIngest : INotificationEventIngest
{
    /// <summary>
    /// El insert del evento. <c>ON CONFLICT DO NOTHING</c> <b>sin nombrar índice</b> a propósito:
    /// cubre los dos por los que un evento puede estar repetido —<c>sg_event_id</c> y la clave de
    /// reserva— con una sola cláusula, y no hay que acertar cuál de los dos choca primero.
    /// </summary>
    private const string InsertEventSql = """
        INSERT INTO notifications.notification_event
            (id, sg_event_id, event_type, occurred_at, payload, recipient_id, dedupe_key)
        VALUES (@id, @sgEventId, @eventType, @occurredAt, @payload, @recipientId, @dedupeKey)
        ON CONFLICT DO NOTHING
        """;

    /// <summary>
    /// El estado actual de la fila, tomado con <c>FOR UPDATE</c>: entre leerlo y escribirlo no
    /// puede meterse otro lote del proveedor, que es exactamente lo que un webhook at-least-once y
    /// desordenado va a intentar.
    /// </summary>
    private const string LockRecipientSql = """
        SELECT delivery_status, recipient_action
        FROM notifications.notification_recipient
        WHERE id = @id
        FOR UPDATE
        """;

    private const string UpdateRecipientSql = """
        UPDATE notifications.notification_recipient
        SET delivery_status = @status,
            recipient_action = @action
        WHERE id = @id
        """;

    /// <summary>Si ese GUID es una fila de este entorno. Stage y producción comparten cuenta.</summary>
    private const string RecipientExistsSql =
        "SELECT 1 FROM notifications.notification_recipient WHERE id = @id";

    /// <summary>
    /// Los envíos abiertos hacia una dirección. Se piden dos: con uno se enlaza, con dos ya no hay
    /// nada que decidir, y traer la lista entera solo costaría filas que no se van a mirar.
    /// </summary>
    private const string OpenSendsSql = """
        SELECT id
        FROM notifications.notification_recipient
        WHERE lower(email) = @email
          AND delivery_status = ANY(@open)
        ORDER BY queued_at DESC, id
        LIMIT 2
        """;

    private const string SuppressSql = """
        INSERT INTO notifications.notification_suppression (email, reason, created_at)
        VALUES (@email, @reason, @now)
        ON CONFLICT (email) DO NOTHING
        """;

    /// <summary>
    /// Los estados abiertos de <see cref="EventCorrelation"/> como arreglo, que es la forma que
    /// Npgsql le pasa al <c>= ANY(...)</c> de la consulta.
    /// </summary>
    private static readonly string[] OpenStatusParameter = [.. EventCorrelation.OpenStatuses];

    private readonly NotificationsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationEventIngest> _logger;

    public NotificationEventIngest(
        NotificationsDbContext context,
        TimeProvider timeProvider,
        ILogger<NotificationEventIngest> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<EventIngestSummary, BaseError>> IngestAsync(
        IReadOnlyList<SendGridEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        return DbResult.TryAsync(_logger, "guardar los eventos del webhook", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var stored = 0;
            var duplicates = 0;
            var orphans = 0;
            var suppressed = 0;

            foreach (var webhookEvent in events)
            {
                var recipientId = await CorrelateAsync(transaction, webhookEvent, cancellationToken);

                if (!await InsertAsync(transaction, webhookEvent, recipientId, cancellationToken))
                {
                    // Ya estaba. No es un fallo: es el reintento del proveedor haciendo lo que se
                    // diseñó para que hiciera.
                    duplicates++;

                    continue;
                }

                stored++;

                if (recipientId is { } id)
                    await ApplyAsync(transaction, id, webhookEvent.EventType, cancellationToken);
                else
                    orphans++;

                if (await SuppressAsync(transaction, webhookEvent, cancellationToken))
                    suppressed++;
            }

            await transaction.CommitAsync(cancellationToken);

            return new EventIngestSummary(stored, duplicates, orphans, suppressed);
        });
    }

    /// <summary>
    /// La escalera de tres peldaños. La decisión es de <see cref="EventCorrelation"/>; acá solo se
    /// buscan los datos con los que la toma.
    /// </summary>
    private async Task<Guid?> CorrelateAsync(
        IDbContextTransaction transaction,
        SendGridEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var exists = webhookEvent.CorrelationId is { } correlationId
            && await ExistsAsync(transaction, correlationId, cancellationToken);

        // Solo se paga la consulta por dirección cuando no hay GUID: un GUID que no existe es
        // huérfano y no baja al segundo peldaño.
        var openSends = webhookEvent.CorrelationId is null && !string.IsNullOrWhiteSpace(webhookEvent.Email)
            ? await OpenSendsAsync(transaction, webhookEvent.Email, cancellationToken)
            : [];

        return EventCorrelation.Resolve(webhookEvent.CorrelationId, exists, openSends);
    }

    private static async Task<bool> ExistsAsync(
        IDbContextTransaction transaction,
        Guid recipientId,
        CancellationToken cancellationToken)
    {
        await using var command = TransactionCommands.Create(transaction, RecipientExistsSql);
        command.Parameters.AddWithValue("id", recipientId);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<IReadOnlyList<Guid>> OpenSendsAsync(
        IDbContextTransaction transaction,
        string email,
        CancellationToken cancellationToken)
    {
        await using var command = TransactionCommands.Create(transaction, OpenSendsSql);
        command.Parameters.AddWithValue("email", email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("open", OpenStatusParameter);

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetGuid(0));

        return ids;
    }

    /// <summary>Guarda el evento. Devuelve <c>false</c> si ya estaba.</summary>
    private static async Task<bool> InsertAsync(
        IDbContextTransaction transaction,
        SendGridEvent webhookEvent,
        Guid? recipientId,
        CancellationToken cancellationToken)
    {
        await using var command = TransactionCommands.Create(transaction, InsertEventSql);

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("sgEventId", (object?)webhookEvent.SgEventId ?? DBNull.Value);
        command.Parameters.AddWithValue("eventType", webhookEvent.EventType);
        command.Parameters.AddWithValue("recipientId", (object?)recipientId ?? DBNull.Value);
        command.Parameters.AddWithValue("dedupeKey", (object?)webhookEvent.DedupeKey ?? DBNull.Value);

        // El JSON crudo entra tipado como jsonb: sin el tipo, Npgsql lo mandaría como texto y
        // PostgreSQL no admite la conversión implícita.
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
        {
            Value = webhookEvent.Payload
        });

        TransactionCommands.AddTimestamp(command, "occurredAt", webhookEvent.OccurredAt);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    /// <summary>
    /// Aplica el evento sobre la fila: el estado que más lejos llega de los dos, nunca una
    /// asignación. Es lo que deja que un <c>processed</c> tardío no degrade un <c>delivered</c>.
    /// </summary>
    private static async Task ApplyAsync(
        IDbContextTransaction transaction,
        Guid recipientId,
        string eventType,
        CancellationToken cancellationToken)
    {
        string currentStatus;
        string currentAction;

        await using (var locking = TransactionCommands.Create(transaction, LockRecipientSql))
        {
            locking.Parameters.AddWithValue("id", recipientId);

            await using var reader = await locking.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return;

            currentStatus = reader.GetString(0);
            currentAction = reader.GetString(1);
        }

        var status = DeliveryLadder.Advance(currentStatus, DeliveryLadder.DeliveryStatusFor(eventType));
        var action = DeliveryLadder.AdvanceAction(currentAction, DeliveryLadder.RecipientActionFor(eventType));

        if (status == currentStatus && action == currentAction)
            return;

        await using var update = TransactionCommands.Create(transaction, UpdateRecipientSql);

        update.Parameters.AddWithValue("id", recipientId);
        update.Parameters.AddWithValue("status", status);
        update.Parameters.AddWithValue("action", action);

        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Retira la dirección de circulación si el evento lo pide. Devuelve <c>true</c> solo si la
    /// dirección no estaba ya retirada, para que el resumen del log cuente lo que cambió.
    /// </summary>
    private async Task<bool> SuppressAsync(
        IDbContextTransaction transaction,
        SendGridEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (DeliveryLadder.SuppressionReasonFor(webhookEvent.EventType) is not { } reason
            || string.IsNullOrWhiteSpace(webhookEvent.Email))
        {
            return false;
        }

        await using var command = TransactionCommands.Create(transaction, SuppressSql);

        command.Parameters.AddWithValue("email", webhookEvent.Email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("reason", reason);

        TransactionCommands.AddTimestamp(command, "now", _timeProvider.GetUtcNow());

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

}

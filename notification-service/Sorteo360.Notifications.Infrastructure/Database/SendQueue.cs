using System.Data.Common;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Core.Sending;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;
using Npgsql;
using NpgsqlTypes;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <inheritdoc cref="ISendQueue" />
public sealed class SendQueue : ISendQueue
{
    /// <summary>
    /// Toma la siguiente fila pendiente y la deja bloqueada hasta que termine la transacción.
    /// <para>
    /// <c>FOR UPDATE SKIP LOCKED</c> es lo que hace que dos drenadores no se pisen: el segundo no
    /// espera a que el primero suelte la fila, se la salta y va a la siguiente. Sin la cláusula,
    /// dos réplicas mandarían cada correo dos veces.
    /// </para>
    /// <para>
    /// Pendiente es <c>queued</c> y con la hora cumplida. Los demás estados no son nuestros:
    /// <c>deferred</c> lo escribe el proveedor por su webhook y lo reintenta él, no nosotros.
    /// </para>
    /// </summary>
    private const string ClaimSql = """
        WITH claimed AS (
            SELECT r.id
            FROM notifications.notification_recipient r
            WHERE r.delivery_status = @queued
              AND (r.next_attempt_at IS NULL OR r.next_attempt_at <= @now)
            ORDER BY r.queued_at, r.id
            FOR UPDATE SKIP LOCKED
            LIMIT 1
        )
        SELECT r.id, r.email, r.name, r.attempt_count, r.queued_at, r.batch_id,
               b.from_address, b.from_name, b.subject, b.body,
               p.render_values
        FROM claimed c
        JOIN notifications.notification_recipient r ON r.id = c.id
        JOIN notifications.notification_batch b ON b.id = r.batch_id
        LEFT JOIN notifications.notification_payload p ON p.recipient_id = r.id
        """;

    /// <summary>
    /// Escribe el resultado del intento. El estado se escribe fila a fila, en cuanto termina su
    /// transacción SMTP: un lote puede morir a la mitad, y una sola escritura al final perdería lo
    /// que ya salió.
    /// </summary>
    private const string SettleSql = """
        UPDATE notifications.notification_recipient
        SET delivery_status = @status,
            attempt_count   = attempt_count + @attemptIncrement,
            next_attempt_at = @nextAttemptAt,
            sent_at         = COALESCE(@sentAt, sent_at),
            last_error      = @lastError,
            smtp_queued_as  = COALESCE(@smtpQueuedAs, smtp_queued_as)
        WHERE id = @id
        """;

    /// <summary>
    /// Borra los valores de sustitución. Se hace en la misma transacción que deja la fila en un
    /// estado terminal, así que el instante en que el envío deja de poder reintentarse es el mismo
    /// en que su token deja de existir.
    /// </summary>
    private const string DeletePayloadSql =
        "DELETE FROM notifications.notification_payload WHERE recipient_id = @id";

    private readonly NotificationsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SendQueue> _logger;

    public SendQueue(NotificationsDbContext context, TimeProvider timeProvider, ILogger<SendQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<bool, BaseError>> TryDrainNextAsync(
        Func<PendingSend, CancellationToken, Task<SendOutcome>> send,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);

        // Como toda escritura del servicio, va dentro del seam del chasis: lo que falle en la base
        // sale como BaseError y ninguna excepción de EF llega a cruzar hacia el worker.
        return DbResult.TryResultAsync(_logger, "drenar la cola de envíos", async () =>
        {
            var now = _timeProvider.GetUtcNow();

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var pending = await ClaimAsync(transaction, now, cancellationToken);

            if (pending is null)
            {
                await transaction.RollbackAsync(cancellationToken);

                return Result.Success<bool, BaseError>(false);
            }

            // Lo que devuelva es lo que se escribe, incluido un fallo que no supo clasificar:
            // clasificarlo es del worker, y es él quien se encarga de que ninguna fila se quede sin
            // avanzar. Acá no hay nada que decidir.
            var outcome = await send(pending, cancellationToken);

            await SettleAsync(transaction, pending, outcome, _timeProvider.GetUtcNow(), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success<bool, BaseError>(true);
        });
    }

    /// <summary>Lee la fila bloqueada, o null si no hay ninguna que toque.</summary>
    private async Task<PendingSend?> ClaimAsync(
        IDbContextTransaction transaction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = TransactionCommands.Create(transaction, ClaimSql);

        command.Parameters.AddWithValue("queued", DeliveryStatus.Queued);
        TransactionCommands.AddTimestamp(command, "now", now);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        // Las columnas se piden por nombre y no por posición: con ordinales sueltos, reordenar el
        // SELECT de arriba rompe esto en silencio y el error aparece como un dato equivocado.
        var renderValues = reader.GetOrdinal("render_values");

        // Sin fila de valores no hay nada que sustituir. Pasa cuando el worker recoge una fila
        // cuyos valores ya se borraron, que solo puede ocurrir si algo terminó el envío por otra
        // vía: se manda con el diccionario vacío, y el cuerpo sale como esté.
        var values = reader.IsDBNull(renderValues)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : Deserialize(reader.GetString(renderValues));

        var fromName = reader.GetOrdinal("from_name");
        var name = reader.GetOrdinal("name");

        return new PendingSend(
            RecipientId: reader.GetGuid(reader.GetOrdinal("id")),
            Email: reader.GetString(reader.GetOrdinal("email")),
            Name: reader.IsDBNull(name) ? null : reader.GetString(name),
            AttemptCount: reader.GetInt32(reader.GetOrdinal("attempt_count")),
            QueuedAt: ReadTimestamp(reader, reader.GetOrdinal("queued_at")),
            BatchId: reader.GetGuid(reader.GetOrdinal("batch_id")),
            FromAddress: reader.GetString(reader.GetOrdinal("from_address")),
            FromName: reader.IsDBNull(fromName) ? null : reader.GetString(fromName),
            Subject: reader.GetString(reader.GetOrdinal("subject")),
            Body: reader.GetString(reader.GetOrdinal("body")),
            Values: values);
    }

    /// <summary>Escribe el resultado y, si el envío ya no se vuelve a tocar, borra sus valores.</summary>
    private static async Task SettleAsync(
        IDbContextTransaction transaction,
        PendingSend pending,
        SendOutcome outcome,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using (var command = TransactionCommands.Create(transaction, SettleSql))
        {
            command.Parameters.AddWithValue("id", pending.RecipientId);
            command.Parameters.AddWithValue("status", outcome.DeliveryStatus);
            command.Parameters.AddWithValue("attemptIncrement", outcome.CountsAsAttempt ? 1 : 0);
            command.Parameters.AddWithValue("lastError", (object?)outcome.LastError ?? DBNull.Value);
            command.Parameters.AddWithValue("smtpQueuedAs", (object?)outcome.SmtpQueuedAs ?? DBNull.Value);
            TransactionCommands.AddTimestamp(command, "nextAttemptAt", outcome.NextAttemptAt);
            TransactionCommands.AddTimestamp(
                command,
                "sentAt",
                outcome.DeliveryStatus == DeliveryStatus.Sent ? now : null);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!outcome.IsTerminal)
            return;

        await using var delete = TransactionCommands.Create(transaction, DeletePayloadSql);
        delete.Parameters.AddWithValue("id", pending.RecipientId);

        await delete.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset ReadTimestamp(DbDataReader reader, int ordinal) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc), TimeSpan.Zero);

    private static Dictionary<string, string> Deserialize(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json, NotificationPayloadData.Json)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
}

using Sorteo360.Notifications.Core.Notifications;
using Npgsql;

namespace Sorteo360.Notifications.Tests.Integration.History;

/// <summary>
/// Siembra el registro en SQL, por nombre de columna. Escribir así y no por la API es a propósito:
/// el historial tiene que poder mostrar filas que hoy nadie sabe escribir todavía —una entrega
/// confirmada, un rebote, un evento huérfano— y esas las escribirá el webhook en su propio ticket.
/// </summary>
internal sealed class NotificationHistorySeed
{
    private readonly string _connectionString;

    public NotificationHistorySeed(string connectionString) => _connectionString = connectionString;

    /// <summary>
    /// Deja vacías las cuatro tablas del registro. Los tests comparten el contenedor, y un total
    /// paginado no dice nada si arrastra las filas del test anterior.
    /// <para>
    /// <c>CASCADE</c> y no una lista cerrada: cualquier tabla que cuelgue de una fila de persona
    /// se vacía con ella. Sin eso, la primera tabla nueva con una clave foránea hacia acá haría
    /// fallar este método y con él toda la suite, en un sitio que no habla de ella.
    /// </para>
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            TRUNCATE notifications.notification_event,
                     notifications.notification_recipient,
                     notifications.notification_about,
                     notifications.notification_batch
            CASCADE
            """,
            connection);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Un envío con sus entidades.</summary>
    public async Task<Guid> AddBatchAsync(
        string type,
        IReadOnlyList<(string SourceService, string EntityType, string EntityId)> about,
        string subject = "Cotización pendiente",
        string body = "<html><p>Hola {{nombre}}</p></html>",
        string language = "es",
        bool trackingExpected = true,
        DateTimeOffset? createdAt = null)
    {
        var id = Guid.NewGuid();
        var when = createdAt ?? DateTimeOffset.UtcNow;

        await using var connection = await OpenAsync();

        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO notifications.notification_batch
                (id, type, from_address, from_name, subject, body, language,
                 idempotency_key, body_sha256, tracking_expected, trace_id, created_at)
            VALUES (@id, @type, @from, @fromName, @subject, @body, @language,
                    @key, @sha, @tracking, NULL, @createdAt)
            """,
            connection))
        {
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("type", type);
            command.Parameters.AddWithValue("from", "no-reply@sorteo360.local");
            command.Parameters.AddWithValue("fromName", "MITUR");
            command.Parameters.AddWithValue("subject", subject);
            command.Parameters.AddWithValue("body", body);
            command.Parameters.AddWithValue("language", language);
            command.Parameters.AddWithValue("key", $"tests:history:{id}");
            command.Parameters.AddWithValue("sha", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant());
            command.Parameters.AddWithValue("tracking", trackingExpected);
            command.Parameters.AddWithValue("createdAt", when);

            await command.ExecuteNonQueryAsync();
        }

        foreach (var (sourceService, entityType, entityId) in about)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO notifications.notification_about
                    (id, batch_id, source_service, entity_type, entity_id)
                VALUES (@id, @batchId, @source, @entityType, @entityId)
                """,
                connection);

            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("batchId", id);
            command.Parameters.AddWithValue("source", sourceService);
            command.Parameters.AddWithValue("entityType", entityType);
            command.Parameters.AddWithValue("entityId", entityId);

            await command.ExecuteNonQueryAsync();
        }

        return id;
    }

    /// <summary>
    /// Una persona del envío, con el estado que se quiera probar. El <paramref name="id"/> se puede
    /// fijar: el lote capturado del webhook nombra GUIDs concretos, y reescribir el archivo para
    /// que coincidieran con unos generados rompería el "byte por byte" que ese test comprueba.
    /// </summary>
    public async Task<Guid> AddRecipientAsync(
        Guid batchId,
        string email,
        string deliveryStatus = DeliveryStatus.Queued,
        string recipientAction = RecipientAction.None,
        DateTimeOffset? queuedAt = null,
        DateTimeOffset? sentAt = null,
        int attemptCount = 0,
        string? lastError = null,
        string? sgMessageId = null,
        string? smtpQueuedAs = null,
        Guid? id = null,
        string? name = null)
    {
        var recipientId = id ?? Guid.NewGuid();

        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO notifications.notification_recipient
                (id, batch_id, email, name, delivery_status, recipient_action, attempt_count,
                 next_attempt_at, queued_at, sent_at, last_error, sg_message_id, smtp_queued_as)
            VALUES (@id, @batchId, @email, @name, @status, @action, @attempts,
                    NULL, @queuedAt, @sentAt, @lastError, @sgMessageId, @queuedAs)
            """,
            connection);

        command.Parameters.AddWithValue("id", recipientId);
        command.Parameters.AddWithValue("batchId", batchId);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("status", deliveryStatus);
        command.Parameters.AddWithValue("action", recipientAction);
        command.Parameters.AddWithValue("attempts", attemptCount);
        command.Parameters.AddWithValue("queuedAt", queuedAt ?? DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("sentAt", (object?)sentAt ?? DBNull.Value);
        command.Parameters.AddWithValue("lastError", (object?)lastError ?? DBNull.Value);
        command.Parameters.AddWithValue("sgMessageId", (object?)sgMessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("queuedAs", (object?)smtpQueuedAs ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();

        return recipientId;
    }

    /// <summary>
    /// Un evento del proveedor. Con <paramref name="recipientId"/> nulo es un huérfano; es lo que
    /// escribirá el webhook cuando no logre correlacionarlo.
    /// </summary>
    public async Task<Guid> AddEventAsync(
        string eventType,
        Guid? recipientId = null,
        string? sgEventId = null,
        string? payload = null,
        DateTimeOffset? occurredAt = null)
    {
        var id = Guid.NewGuid();

        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO notifications.notification_event
                (id, sg_event_id, event_type, occurred_at, payload, recipient_id)
            VALUES (@id, @sgEventId, @eventType, @occurredAt, @payload::jsonb, @recipientId)
            """,
            connection);

        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("sgEventId", (object?)sgEventId ?? DBNull.Value);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("occurredAt", occurredAt ?? DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("payload", payload ?? $$"""{"event":"{{eventType}}"}""");
        command.Parameters.AddWithValue("recipientId", (object?)recipientId ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();

        return id;
    }

    /// <summary>
    /// Mueve el estado de una fila como lo haría el webhook, y deja el evento que lo justifica.
    /// Las dos cosas juntas: mover la columna sin el evento no probaría nada del camino real.
    /// </summary>
    public async Task ApplyProviderEventAsync(Guid recipientId, string eventType, string deliveryStatus)
    {
        await using (var connection = await OpenAsync())
        await using (var command = new NpgsqlCommand(
            """
            UPDATE notifications.notification_recipient
               SET delivery_status = @status
             WHERE id = @id
            """,
            connection))
        {
            command.Parameters.AddWithValue("status", deliveryStatus);
            command.Parameters.AddWithValue("id", recipientId);

            await command.ExecuteNonQueryAsync();
        }

        await AddEventAsync(eventType, recipientId, sgEventId: Guid.NewGuid().ToString());
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        return connection;
    }
}

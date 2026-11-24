using Npgsql;

namespace Sorteo360.Notifications.Tests.Integration.NotificationEndpoints;

/// <summary>
/// Lecturas directas contra la base del contenedor, en SQL y por nombre de columna. Lo que un test
/// necesita comprobar es lo que quedó escrito en las columnas, no lo que el modelo de EF cree.
/// </summary>
internal sealed class NotificationsDatabase
{
    /// <summary>Las cuatro tablas del registro, para las comprobaciones que barren todo.</summary>
    private static readonly string[] Tables =
    [
        "notification_batch", "notification_about", "notification_recipient", "notification_event"
    ];

    /// <summary>
    /// El registro más la tabla efímera de valores. <c>notification_payload</c> es la única del
    /// servicio que contiene un valor de <c>data</c>, y solo mientras su envío sigue siendo
    /// reintentable: se borra en la misma transacción que deja la fila en un estado terminal. Este
    /// barrido es el que comprueba que ahí no quedó nada después de mandar.
    /// </summary>
    private static readonly string[] TablesIncludingPayload = [.. Tables, "notification_payload"];

    private readonly string _connectionString;

    public NotificationsDatabase(string connectionString) => _connectionString = connectionString;

    /// <summary>Cuántos envíos hay con esa clave de idempotencia. Es el "no creó nada" de los tests.</summary>
    public Task<long> CountBatchesAsync(string idempotencyKey) =>
        ScalarAsync<long>(
            "SELECT count(*) FROM notifications.notification_batch WHERE idempotency_key = @key",
            ("key", idempotencyKey));

    public Task<long> CountAboutAsync(Guid batchId) =>
        ScalarAsync<long>(
            "SELECT count(*) FROM notifications.notification_about WHERE batch_id = @id",
            ("id", batchId));

    public Task<long> CountRecipientsAsync(Guid batchId) =>
        ScalarAsync<long>(
            "SELECT count(*) FROM notifications.notification_recipient WHERE batch_id = @id",
            ("id", batchId));

    public Task<string> BodyAsync(Guid batchId) =>
        ScalarAsync<string>("SELECT body FROM notifications.notification_batch WHERE id = @id", ("id", batchId));

    public Task<string> SubjectAsync(Guid batchId) =>
        ScalarAsync<string>("SELECT subject FROM notifications.notification_batch WHERE id = @id", ("id", batchId));

    public Task<bool> TrackingExpectedAsync(Guid batchId) =>
        ScalarAsync<bool>(
            "SELECT tracking_expected FROM notifications.notification_batch WHERE id = @id",
            ("id", batchId));

    /// <summary>Los ids de las filas de persona de un envío.</summary>
    public async Task<IReadOnlyList<Guid>> RecipientIdsAsync(Guid batchId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id FROM notifications.notification_recipient WHERE batch_id = @id ORDER BY id",
            connection);

        command.Parameters.AddWithValue("id", batchId);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));

        return ids;
    }

    /// <summary>
    /// Cada fila de las cuatro tablas volcada como texto. Convertir la fila entera es lo que hace
    /// que la comprobación cubra <b>todas</b> las columnas, incluidas las que se agreguen después.
    /// </summary>
    public Task<IReadOnlyList<string>> EveryRowAsTextAsync() => DumpAsync(Tables);

    /// <summary>Lo mismo, incluida la tabla efímera de valores.</summary>
    public Task<IReadOnlyList<string>> EveryRowIncludingPayloadAsTextAsync() =>
        DumpAsync(TablesIncludingPayload);

    /// <summary>Los ids de historial de una dirección.</summary>
    public async Task<IReadOnlyList<Guid>> RecipientIdsByEmailAsync(string email)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id FROM notifications.notification_recipient WHERE email = @email",
            connection);

        command.Parameters.AddWithValue("email", email);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));

        return ids;
    }

    /// <summary>Cuántas filas de valores le quedan a un envío. Cero cuando ya no se puede reintentar.</summary>
    public Task<long> CountPayloadsAsync(Guid batchId) =>
        ScalarAsync<long>(
            """
            SELECT count(*)
            FROM notifications.notification_payload p
            JOIN notifications.notification_recipient r ON r.id = p.recipient_id
            WHERE r.batch_id = @id
            """,
            ("id", batchId));

    /// <summary>El estado de cada persona de un envío, por dirección.</summary>
    public async Task<IReadOnlyDictionary<string, RecipientRow>> RecipientsAsync(Guid batchId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT email, delivery_status, attempt_count, last_error, smtp_queued_as, sent_at, next_attempt_at
            FROM notifications.notification_recipient
            WHERE batch_id = @id
            """,
            connection);

        command.Parameters.AddWithValue("id", batchId);

        var rows = new Dictionary<string, RecipientRow>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows[reader.GetString(0)] = new RecipientRow(
                DeliveryStatus: reader.GetString(1),
                AttemptCount: reader.GetInt32(2),
                LastError: reader.IsDBNull(3) ? null : reader.GetString(3),
                SmtpQueuedAs: reader.IsDBNull(4) ? null : reader.GetString(4),
                SentAt: reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                NextAttemptAt: reader.IsDBNull(6) ? null : reader.GetDateTime(6));
        }

        return rows;
    }

    /// <summary>Suprime una dirección, como lo hará el webhook de eventos cuando llegue.</summary>
    public async Task SuppressAsync(string email, string reason)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO notifications.notification_suppression (email, reason, created_at)
            VALUES (@email, @reason, now())
            ON CONFLICT (email) DO UPDATE SET reason = EXCLUDED.reason
            """,
            connection);

        command.Parameters.AddWithValue("email", email.ToLowerInvariant());
        command.Parameters.AddWithValue("reason", reason);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Vacía la lista de supresión. La base se comparte entre tests de una misma clase.</summary>
    public async Task ClearSuppressionsAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM notifications.notification_suppression",
            connection);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Envejece un envío para que la ventana de una hora ya haya pasado.</summary>
    public async Task BackdateQueuedAtAsync(Guid batchId, TimeSpan by)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE notifications.notification_recipient
            SET queued_at = queued_at - @by
            WHERE batch_id = @id
            """,
            connection);

        command.Parameters.AddWithValue("id", batchId);
        command.Parameters.AddWithValue("by", by);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Adelanta la hora del próximo intento para que el worker vuelva a recoger la fila.</summary>
    public async Task MakeDueAsync(Guid batchId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE notifications.notification_recipient
            SET next_attempt_at = NULL
            WHERE batch_id = @id
            """,
            connection);

        command.Parameters.AddWithValue("id", batchId);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>El nombre guardado de cada persona de un envío, por dirección.</summary>
    public async Task<IReadOnlyDictionary<string, string?>> RecipientNamesAsync(Guid batchId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT email, name FROM notifications.notification_recipient WHERE batch_id = @id",
            connection);

        command.Parameters.AddWithValue("id", batchId);

        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            names[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);

        return names;
    }

    private async Task<IReadOnlyList<string>> DumpAsync(IReadOnlyList<string> tables)
    {
        var rows = new List<string>();

        await using var connection = await OpenAsync();

        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT t::text FROM notifications.{table} t",
                connection);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var scalar = await command.ExecuteScalarAsync();

        return (T)scalar!;
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        return connection;
    }

}

/// <summary>Una fila de persona leída por columnas, tal como quedó escrita.</summary>
internal sealed record RecipientRow(
    string DeliveryStatus,
    int AttemptCount,
    string? LastError,
    string? SmtpQueuedAs,
    DateTime? SentAt,
    DateTime? NextAttemptAt);

using Npgsql;

namespace Sorteo360.Notifications.Tests.Integration.Webhooks;

/// <summary>
/// Lecturas directas de lo que el webhook dejó escrito, en SQL y por nombre de columna: lo que hay
/// que comprobar son las columnas, no lo que el modelo de EF cree.
/// </summary>
internal sealed class WebhookDatabase
{
    private readonly string _connectionString;

    public WebhookDatabase(string connectionString) => _connectionString = connectionString;

    /// <summary>Cuántos eventos hay en total.</summary>
    public Task<long> CountEventsAsync() =>
        ScalarAsync<long>("SELECT count(*) FROM notifications.notification_event");

    /// <summary>Cuántos eventos quedaron sin enlazar.</summary>
    public Task<long> CountOrphansAsync() =>
        ScalarAsync<long>("SELECT count(*) FROM notifications.notification_event WHERE recipient_id IS NULL");

    /// <summary>Cuántos eventos se enlazaron con esa fila de persona.</summary>
    public Task<long> CountEventsForAsync(Guid recipientId) =>
        ScalarAsync<long>(
            "SELECT count(*) FROM notifications.notification_event WHERE recipient_id = @id",
            ("id", recipientId));

    /// <summary>Los tipos de evento guardados, incluidos los que no reconocemos.</summary>
    public async Task<IReadOnlyList<string>> EventTypesAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT event_type FROM notifications.notification_event ORDER BY occurred_at, event_type",
            connection);

        var types = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            types.Add(reader.GetString(0));

        return types;
    }

    /// <summary>El estado de entrega y la reacción de una fila, tal como quedaron.</summary>
    public async Task<(string DeliveryStatus, string RecipientAction)> RecipientAsync(Guid recipientId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT delivery_status, recipient_action
            FROM notifications.notification_recipient
            WHERE id = @id
            """,
            connection);

        command.Parameters.AddWithValue("id", recipientId);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return (reader.GetString(0), reader.GetString(1));
    }

    /// <summary>El motivo por el que una dirección está retirada, o null si no lo está.</summary>
    public async Task<string?> SuppressionReasonAsync(string email)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT reason FROM notifications.notification_suppression WHERE email = @email",
            connection);

        command.Parameters.AddWithValue("email", email.ToLowerInvariant());

        return await command.ExecuteScalarAsync() as string;
    }

    /// <summary>Vacía las tablas que el webhook toca. Los tests comparten el contenedor.</summary>
    public async Task ResetAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            TRUNCATE notifications.notification_event,
                     notifications.notification_payload,
                     notifications.notification_recipient,
                     notifications.notification_about,
                     notifications.notification_batch
            CASCADE;
            DELETE FROM notifications.notification_suppression;
            """,
            connection);

        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        return connection;
    }
}

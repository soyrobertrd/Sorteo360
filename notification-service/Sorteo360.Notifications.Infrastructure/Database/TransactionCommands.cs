using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Comandos SQL sobre la transacción en curso de un <c>DbContext</c>.
/// <para>
/// Las dos escrituras crudas del servicio —la cola de envíos y la ingesta de eventos del webhook—
/// necesitan lo mismo: un comando enganchado a la conexión <b>de la transacción</b>, que es lo que
/// mantiene el bloqueo de la fila y su escritura en la misma unidad de trabajo, y marcas de tiempo
/// tipadas. Estaba escrito dos veces; acá está una.
/// </para>
/// </summary>
internal static class TransactionCommands
{
    /// <summary>
    /// Un comando sobre la conexión de la transacción en curso. Pedirlo así y no al
    /// <c>DbContext</c> es lo que lo pone dentro de la transacción en vez de al lado.
    /// </summary>
    public static NpgsqlCommand Create(IDbContextTransaction transaction, string sql)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var dbTransaction = transaction.GetDbTransaction();
        var command = (NpgsqlCommand)dbTransaction.Connection!.CreateCommand();

        command.CommandText = sql;
        command.Transaction = (NpgsqlTransaction)dbTransaction;

        return command;
    }

    /// <summary>
    /// Agrega una marca de tiempo tipada. Se declara el tipo en vez de dejarlo inferir porque un
    /// null sin tipo no se puede resolver contra una columna <c>timestamptz</c>.
    /// </summary>
    public static void AddTimestamp(NpgsqlCommand command, string name, DateTimeOffset? value)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.TimestampTz)
        {
            Value = value is null ? DBNull.Value : value.Value.UtcDateTime
        });
    }
}

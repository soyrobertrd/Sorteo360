using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// Guardar un lote del webhook y recalcular las filas que toca.
/// <para>
/// <b>Todo el lote en una transacción, y la respuesta en línea.</b> No hay cola intermedia: se
/// insertan los eventos, se recalculan las filas afectadas, se confirma y se contesta 200. Lo
/// justifican el volumen —el MITUR no es un emisor masivo— y la deduplicación: si el proveedor no
/// llega a ver nuestra respuesta y reintenta, el reintento no hace nada, así que una respuesta
/// lenta no cuesta más que tiempo. Se revisa el día que un lote se mida lento, y no antes.
/// </para>
/// </summary>
public interface INotificationEventIngest
{
    /// <summary>
    /// Guarda los eventos que todavía no estaban y aplica lo que digan. Devuelve el resumen del
    /// lote, que es lo que se registra en el log: quien llama no recibe detalle, porque el
    /// proveedor no es un cliente al que explicarle nada.
    /// </summary>
    Task<Result<EventIngestSummary, BaseError>> IngestAsync(
        IReadOnlyList<SendGridEvent> events,
        CancellationToken cancellationToken);
}

/// <summary>Qué pasó con un lote, para el log.</summary>
/// <param name="Stored">Eventos nuevos guardados.</param>
/// <param name="Duplicates">Eventos que ya estaban, y por eso no hicieron nada.</param>
/// <param name="Orphans">Eventos guardados sin fila de persona a la que enlazarlos.</param>
/// <param name="Suppressed">Direcciones que este lote retiró de circulación.</param>
public sealed record EventIngestSummary(int Stored, int Duplicates, int Orphans, int Suppressed);

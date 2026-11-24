using CSharpFunctionalExtensions;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Database;

/// <summary>
/// Escritura del registro de un envío aceptado.
/// </summary>
public interface INotificationBatchRepository
{
    /// <summary>
    /// Guarda el envío, sus entidades y sus personas en una sola transacción.
    /// <para>
    /// Si la clave de idempotencia ya existe no escribe nada y devuelve el envío que ya estaba, con
    /// sus ids. La carrera entre dos peticiones idénticas la resuelve el índice único: quien pierde
    /// atrapa la violación, vuelve a leer por la clave y contesta con lo de quien ganó. Sin
    /// candados y sin coordinación.
    /// </para>
    /// </summary>
    /// <param name="send">El envío aceptado entero: el correo, sus entidades, sus personas y sus valores.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<StoredBatch, BaseError>> AddAsync(
        AcceptedSend send,
        CancellationToken cancellationToken);
}

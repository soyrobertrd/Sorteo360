using CSharpFunctionalExtensions;
using Sorteo360.Notifications.Core.DTOs.History;
using Sorteo360.Notifications.Core.History;
using Sorteo360.Notifications.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Database;

/// <summary>
/// Lectura del registro de envíos. Solo lectura: el historial no tiene ninguna escritura que
/// diseñar, porque sin los valores de <c>data</c> un reenvío mandaría un correo distinto del que
/// la pantalla muestra.
/// <para>
/// Toda operación recibe el <see cref="HistoryScope"/> de quien llama y lo aplica ella misma. No
/// es un filtro opcional que un endpoint pueda olvidarse de pasar: es un parámetro obligatorio de
/// cada método.
/// </para>
/// </summary>
public interface INotificationHistoryReader
{
    /// <summary>Una página de filas de persona, de la más reciente a la más vieja.</summary>
    Task<Result<PagedResult<HistoryRecipientDto>, BaseError>> ListRecipientsAsync(
        NotificationHistoryQueryDto query,
        HistoryScope scope,
        CancellationToken cancellationToken);

    /// <summary>
    /// Un envío con su cuerpo sin sustituir, sus entidades y sus personas. Es lo que resuelve la
    /// cabecera <c>Location</c> de un 202. Devuelve vacío si no existe <b>o</b> si el alcance de
    /// quien llama no lo alcanza: la existencia de un envío ajeno tampoco se filtra.
    /// </summary>
    Task<Result<Maybe<HistoryBatchDto>, BaseError>> FindBatchAsync(
        Guid batchId,
        HistoryScope scope,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una página de eventos sin correlacionar. Una cuenta de servicio no ve ninguno: un huérfano
    /// no tiene envío, así que no tiene <c>source_service</c> contra el que acotarlo, y se prefiere
    /// una lista vacía a enseñarle a un servicio los rebotes de otro.
    /// </summary>
    Task<Result<PagedResult<OrphanEventDto>, BaseError>> ListOrphanEventsAsync(
        int page,
        int pageSize,
        HistoryScope scope,
        CancellationToken cancellationToken);
}

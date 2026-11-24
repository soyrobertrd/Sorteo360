namespace Sorteo360.Convocatorias.Core.Notifications;

/// <summary>
/// Lee de notification-service qué pasó con unos correos ya entregados (PGTI-177), por el id de
/// mensaje que devolvió cada envío.
/// <para>
/// <b>Nunca falla.</b> notification-service es el dueño de ese dato y esta lectura solo lo
/// muestra: si no contesta, la fila dice <c>sent</c>, que es cierto, en vez de tumbar una
/// pantalla de seguimiento por un vecino (CLAUDE.md raíz, ADR-0013).
/// </para>
/// </summary>
public interface IDeliveryStatusReader
{
    /// <summary>
    /// Un <see cref="Campaigns.CampaignDeliveryStatus"/> por cada id que notification-service
    /// conoce. Un id que falta en la respuesta se lee como <c>sent</c>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ReadAsync(
        IReadOnlyCollection<Guid> notificationIds,
        CancellationToken cancellationToken);
}

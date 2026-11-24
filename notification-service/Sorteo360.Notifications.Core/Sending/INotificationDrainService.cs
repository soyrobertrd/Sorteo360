namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Vaciar la cola: tomar las filas que tocan, mandarlas y anotar qué pasó con cada una.
/// </summary>
public interface INotificationDrainService
{
    /// <summary>
    /// Drena hasta que no quede nada pendiente y devuelve cuántas filas se resolvieron. Abre una
    /// sola conexión SMTP y la reutiliza para todas; si no hay nada pendiente no llega a abrirla.
    /// </summary>
    Task<int> DrainAsync(CancellationToken cancellationToken);
}

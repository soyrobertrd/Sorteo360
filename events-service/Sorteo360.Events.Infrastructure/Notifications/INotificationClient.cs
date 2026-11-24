namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// La única puerta de notification-service que este servicio usa. Es el único lugar de
/// events-service donde aparece una ruta de notificaciones: el chasis sabe autenticarse y
/// sobrevivir a la red, pero no conoce ninguna ruta ni ningún DTO.
/// <para>
/// Todo fallo de red, de autenticación o de estado sale como <see cref="HttpRequestException"/>,
/// que es lo que el chasis garantiza. Quien llama lo traduce a un estado de aviso; acá no hay
/// política de degradación, solo transporte.
/// </para>
/// </summary>
public interface INotificationClient
{
    /// <summary>
    /// Entrega un lote. El <c>202</c> significa "lo guardamos y lo vamos a intentar": no significa
    /// que el proveedor lo tomó, y nunca significa entregado.
    /// </summary>
    Task<SendNotificationResponse?> SendAsync(SendNotificationRequest request, CancellationToken cancellationToken);
}

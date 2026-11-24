using System.Net.Http.Json;

namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// Cliente tipado de notification-service. El <c>HttpClient</c> lo arma el chasis
/// (<c>AddServiceClient</c>): trae la URL base configurada, la firma con la identidad propia de
/// este servicio y la envuelve en la tubería de resiliencia. Acá solo viven la ruta y la forma de
/// la respuesta.
/// </summary>
public sealed class NotificationClient : INotificationClient
{
    private const string SendRoute = "v1/notifications";

    private readonly HttpClient _httpClient;

    public NotificationClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<SendNotificationResponse?> SendAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(SendRoute, request, cancellationToken);

        // Cualquier estado que no sea de éxito sale como HttpRequestException, igual que una avería
        // de red: desde acá arriba un 400 y un socket cerrado significan lo mismo, que el aviso no
        // salió, y quien llama no tiene forma de actuar distinto sobre uno o el otro.
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SendNotificationResponse>(cancellationToken);
    }
}

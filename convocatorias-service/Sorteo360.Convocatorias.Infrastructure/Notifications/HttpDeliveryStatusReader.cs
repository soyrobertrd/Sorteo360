using System.Net.Http.Json;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Infrastructure.Neighbours;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// La entrega leída del historial de notification-service, <c>GET /v1/notifications?id=</c>, de a
/// cien ids por llamada (PGTI-177). La cuenta de servicio de convocatorias solo alcanza sus propios
/// envíos, que son justo los que pregunta.
/// <para>
/// <b>Se degrada y no falla.</b> Si notification-service no contesta, o contesta algo que no es un
/// 200, lo que se leyó hasta ahí vale y el resto queda en "enviado". Un rebote que llegó sin id de
/// correlación queda huérfano del otro lado, así que su fila sigue en "enviado" también: no hay a
/// quién atribuírselo.
/// </para>
/// </summary>
public sealed class HttpDeliveryStatusReader : IDeliveryStatusReader
{
    private const string NotificationsRoute = "v1/notifications";

    /// <summary>El tope de ids, y de página, del otro lado.</summary>
    private const int MaxIdsPerCall = 100;

    /// <summary>Cuánto espera una llamada antes de rendirse y mostrar "enviado".</summary>
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpDeliveryStatusReader> _logger;

    public HttpDeliveryStatusReader(HttpClient httpClient, ILogger<HttpDeliveryStatusReader> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> ReadAsync(
        IReadOnlyCollection<Guid> notificationIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificationIds);

        var known = new Dictionary<Guid, string>();

        foreach (var chunk in notificationIds.Distinct().Chunk(MaxIdsPerCall))
        {
            var read = await ReadChunkAsync(chunk, cancellationToken);

            if (read.IsFailure)
            {
                _logger.LogWarning(
                    "No se pudo leer la entrega de {Count} correos en notification-service; se muestran como enviados.",
                    notificationIds.Count);

                break;
            }

            foreach (var item in read.Value)
                known[item.Id] = CampaignDeliveryStatus.FromNotification(item.DeliveryStatus, item.RecipientAction);
        }

        return known;
    }

    private Task<Result<IReadOnlyList<NotificationDeliveryItem>, BaseError>> ReadChunkAsync(
        Guid[] ids,
        CancellationToken cancellationToken) =>
        NeighbourCall.GuardAsync<IReadOnlyList<NotificationDeliveryItem>>(
            async () =>
            {
                // Un plazo propio y corto: una pantalla de seguimiento no espera los 30 s del chasis
                // por un dato que solo adorna. Y como la cancelación es de quien llama, el circuito
                // compartido con los envíos no la cuenta como fallo de notification-service.
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                budget.CancelAfter(ReadBudget);

                try
                {
                    var query = string.Join('&', ids.Select(id => $"id={id}"));

                    using var response = await _httpClient.GetAsync(
                        $"{NotificationsRoute}?pageSize={MaxIdsPerCall}&{query}",
                        budget.Token);

                    if (!response.IsSuccessStatusCode)
                        return new NotificationsUnavailableError($"contestó {(int)response.StatusCode}");

                    var page = await response.Content.ReadFromJsonAsync<NotificationDeliveryPage>(budget.Token);

                    return page is null
                        ? new NotificationsUnavailableError("el cuerpo vino vacío")
                        : Result.Success<IReadOnlyList<NotificationDeliveryItem>, BaseError>(page.Items);
                }
                catch (Exception exception) when (
                    exception is JsonException or NotSupportedException
                    || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
                {
                    // Un cuerpo ilegible o el plazo agotado: lo mismo que una caída, se ve "enviado".
                    return new NotificationsUnavailableError(exception.GetType().Name);
                }
            },
            new NotificationsUnavailableError("no respondió al leer la entrega"),
            _logger,
            "notification-service",
            "leer la entrega de {Count} correos",
            cancellationToken,
            ids.Length);
}

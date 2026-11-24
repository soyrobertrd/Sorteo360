using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Infrastructure.Neighbours;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// El transporte transaccional de verdad: <c>POST /v1/notifications</c> contra notification-service
/// (spec <c>04</c> §2), un lote de un destinatario. El <c>HttpClient</c> lo arma el chasis
/// (<c>AddServiceClient</c>), así que la URL base, la identidad propia del servicio y la resiliencia
/// no viven acá; acá viven el cuerpo del cable y la traducción de la avería. Es la implementación
/// hermana de <see cref="HttpCampaignSendGateway"/>.
/// <para>
/// <b>Un fallo no anota nada.</b> A diferencia de una campaña, una confirmación no tiene filas que
/// dejar <c>pending</c> ni un cursor que reanudar: si notification-service no contesta el <c>202</c>,
/// el correo no salió y quien llama lo registra y sigue, porque la inscripción ya está guardada
/// (spec <c>06</c> §4.4). Por eso basta con distinguir el <c>2xx</c> del resto y no hace falta
/// emparejar la respuesta por destinatario.
/// </para>
/// </summary>
public sealed class HttpTransactionalNotifier : ITransactionalNotifier
{
    private const string NotificationsRoute = "v1/notifications";

    /// <summary>Quién manda, en el <c>about</c> del correo.</summary>
    private const string SourceService = "convocatorias";

    private const string EventEntity = "event";

    private readonly HttpClient _httpClient;
    private readonly NotificationSettings _settings;
    private readonly ILogger<HttpTransactionalNotifier> _logger;

    public HttpTransactionalNotifier(
        HttpClient httpClient,
        IOptions<NotificationSettings> settings,
        ILogger<HttpTransactionalNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> SendAsync(
        TransactionalNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return await NeighbourCall.GuardAsync(
            async () =>
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    NotificationsRoute,
                    ToWire(notification),
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    return UnitResult.Success<BaseError>();

                // Antes del 202 no existe historia del otro lado: un 400 o un 5xx son el mismo
                // desenlace de este lado, el correo no salió.
                _logger.LogError(
                    "notification-service rechazó la confirmación del evento {EventId} con {StatusCode}.",
                    notification.EventId,
                    (int)response.StatusCode);

                return Failure($"contestó {(int)response.StatusCode}");
            },
            new NotificationsUnavailableError("no respondió o agotó el tiempo de espera"),
            _logger,
            "notification-service",
            "enviar la confirmación del evento {EventId}",
            cancellationToken,
            notification.EventId);
    }

    /// <summary>
    /// El correo en el cuerpo que la spec <c>04</c> §2 fija. El asunto y el cuerpo van con sus
    /// marcadores intactos; el <c>about</c> es solo el evento —no hay campaña— y la clave de
    /// idempotencia la acuñó quien armó el correo, que es quien sabe qué cuenta como el mismo envío.
    /// </summary>
    private NotificationBatchRequest ToWire(TransactionalNotification notification) =>
        new(
            notification.Type,
            notification.Language,
            _settings.FromAddress,
            _settings.FromName,
            notification.Subject,
            notification.BodyHtml,
            notification.SharedData,
            [new NotificationRecipientRequest(notification.RecipientEmail, notification.RecipientName, notification.RecipientData)],
            [new NotificationAboutRequest(SourceService, EventEntity, notification.EventId.ToString())],
            notification.IdempotencyKey);

    private static UnitResult<BaseError> Failure(string detail) =>
        UnitResult.Failure<BaseError>(new NotificationsUnavailableError(detail));
}

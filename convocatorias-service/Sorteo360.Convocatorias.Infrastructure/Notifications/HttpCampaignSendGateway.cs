using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Infrastructure.Neighbours;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// El transporte de verdad: <c>POST /v1/notifications</c> contra notification-service (spec
/// <c>04</c> §2). El <c>HttpClient</c> lo arma el chasis (<c>AddServiceClient</c>), así que la URL
/// base, la identidad propia de este servicio y la tubería de resiliencia no viven acá; acá viven
/// el cuerpo del cable, el emparejado de la respuesta y la traducción de la avería.
/// <para>
/// <b>Un fallo de acá nunca marca a nadie <c>failed</c>.</b> Esa es la distinción de la que vive el
/// cursor de reanudación (§2.1): <c>failed</c> significa que el emisor miró a esa persona y la
/// rechazó, y cualquier otra cosa deja la fila <c>pending</c> para que una persona la vuelva a
/// mandar por <c>resume-handoff</c>. Este servicio no reintenta solo, a propósito (§5).
/// </para>
/// <para>
/// <b>El caso ambiguo es un tiempo de espera agotado</b>, donde no se puede saber si el lote
/// aterrizó. Los reintentos del chasis mandan el mismo lote con la misma clave, así que el emisor
/// no lo escribe dos veces; pero si se agotan, el lote se da por no entregado, sus filas se quedan
/// <c>pending</c>, y reanudar acuña tokens nuevos: es otro envío, con otra clave, y puede
/// escribirle dos veces a esa tanda. Ver <see cref="NotificationIdempotencyKey.ForCampaignBatch"/>.
/// </para>
/// </summary>
public sealed class HttpCampaignSendGateway : ICampaignSendGateway
{
    private const string NotificationsRoute = "v1/notifications";

    /// <summary>Quién manda, en el <c>about</c> de cada lote.</summary>
    private const string SourceService = "convocatorias";

    private const string CampaignEntity = "campaign";
    private const string EventEntity = "event";

    private readonly HttpClient _httpClient;
    private readonly NotificationSettings _settings;
    private readonly ILogger<HttpCampaignSendGateway> _logger;

    public HttpCampaignSendGateway(
        HttpClient httpClient,
        IOptions<NotificationSettings> settings,
        ILogger<HttpCampaignSendGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<SendBatchResult, BaseError>> SendAsync(
        SendBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Las dos reglas que el receptor cobra, cobradas antes de salir. Llegar a cualquiera de
        // las dos es un fallo de este lado, así que gastar la ida y vuelta para que nos lo digan
        // solo perdería el nombre de lo que pasó.
        var contract = NotificationBatchContract.Validate(request);

        if (contract.IsFailure)
            return Result.Failure<SendBatchResult, BaseError>(contract.Error);

        return await NeighbourCall.GuardAsync(
            async () =>
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    NotificationsRoute,
                    ToWire(request),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // Antes del 202 no existe historia del otro lado: un 400 no anota a nadie, y un
                    // 5xx tampoco. Las dos cosas son el mismo desenlace de este lado.
                    _logger.LogError(
                        "notification-service rechazó el lote de la campaña {CampaignId} con {StatusCode}.",
                        request.CampaignId,
                        (int)response.StatusCode);

                    return Failure($"contestó {(int)response.StatusCode}");
                }

                var batch = await response.Content.ReadFromJsonAsync<NotificationBatchResponse>(cancellationToken);

                if (batch is null)
                    return Unreadable("el cuerpo del 202 vino vacío");

                return Pair(request, batch);
            },
            // Un tiempo agotado es el caso ambiguo de la clase: el lote se da por no entregado y
            // sus filas se quedan pending, aunque pueda haber aterrizado.
            new NotificationsUnavailableError(
                "no respondió o agotó el tiempo de espera; si fue lo segundo, no se sabe si el lote aterrizó"),
            _logger,
            "notification-service",
            "enviar el lote de la campaña {CampaignId}",
            cancellationToken,
            request.CampaignId);
    }

    /// <summary>
    /// El lote en el cuerpo que la spec <c>04</c> §2 fija. El asunto y el cuerpo van con sus
    /// marcadores intactos: sustituir es trabajo del lado que recibe.
    /// </summary>
    private NotificationBatchRequest ToWire(SendBatchRequest request) =>
        new(
            CampaignNotification.Type,
            request.Language,
            // La OPT que lanzó firma con su propia dirección; sin ella, la general. El nombre visible
            // es siempre el de la plataforma (PGTI-168).
            string.IsNullOrWhiteSpace(request.FromAddress) ? _settings.FromAddress : request.FromAddress,
            _settings.FromName,
            request.Subject,
            request.BodyHtml,
            request.SharedData,
            [.. request.Recipients.Select(recipient =>
                new NotificationRecipientRequest(recipient.Email, recipient.Name, recipient.Data))],

            // Las dos mitades de "sobre qué fue este correo": la campaña que lo mandó y el evento
            // que convoca. Con las dos, el historial contesta tanto "qué mandó esta campaña" como
            // "qué salió sobre la Feria de Madrid", que es la pregunta que hizo el cliente. Un
            // mensaje directo no convoca a nada y lleva solo la primera.
            request.EventId is { } eventId
                ?
                [
                    new NotificationAboutRequest(SourceService, CampaignEntity, request.CampaignId.ToString()),
                    new NotificationAboutRequest(SourceService, EventEntity, eventId.ToString()),
                ]
                : [new NotificationAboutRequest(SourceService, CampaignEntity, request.CampaignId.ToString())],

            // La campaña y una huella del lote con sus tokens: el reintento del chasis lleva la
            // misma, y una reanudación, que acuña tokens nuevos, otra.
            NotificationIdempotencyKey.ForCampaignBatch(request));

    /// <summary>
    /// Empareja la respuesta con el lote que salió, <b>por posición</b>, y comprueba que las
    /// direcciones cuadren.
    /// <para>
    /// La respuesta contesta por dirección (§2.1) y la dirección <b>no es nuestra identidad</b>:
    /// dos filas pueden llevar la misma, y entonces buscar por dirección no sabría a cuál de las
    /// dos se refiere un resultado. El orden sí es inequívoco, y comprobar la dirección en cada
    /// posición es lo que impide que un emisor que reordene se lea como si hubiera contestado bien.
    /// </para>
    /// <para>
    /// Una respuesta que no cuadra tumba el lote entero en vez de anotar la parte que sí: media
    /// tanda anotada contra una respuesta que no se entiende es peor que la cola intacta que
    /// reanuda una persona.
    /// </para>
    /// </summary>
    private Result<SendBatchResult, BaseError> Pair(SendBatchRequest request, NotificationBatchResponse batch)
    {
        if (batch.Results.Count != request.Recipients.Count)
        {
            return Unreadable(
                $"contestó {batch.Results.Count} resultados para {request.Recipients.Count} destinatarios");
        }

        var outcomes = new List<RecipientHandoffOutcome>(request.Recipients.Count);

        for (var index = 0; index < batch.Results.Count; index++)
        {
            var recipient = request.Recipients[index];
            var result = batch.Results[index];

            if (!string.Equals(result.Recipient, recipient.Email, StringComparison.OrdinalIgnoreCase))
                return Unreadable($"contestó por otra dirección en la posición {index}");

            if (string.Equals(result.Status, CampaignNotification.StatusAccepted, StringComparison.Ordinal))
            {
                // El id es con lo que después se lee si llegó (PGTI-177). Uno que no parsea no tumba el
                // lote: el correo salió igual, y esa fila solo se quedará en "enviado".
                outcomes.Add(RecipientHandoffOutcome.Accepted(
                    recipient.RecipientId,
                    Guid.TryParse(result.Id, out var notificationId) ? notificationId : null));
                continue;
            }

            if (!string.Equals(result.Status, CampaignNotification.StatusRejected, StringComparison.Ordinal))
                return Unreadable($"contestó el estado '{result.Status}', que no es del contrato");

            // Un rechazo sin motivo sigue siendo un rechazo: el motivo es para quien lee la fila,
            // no la condición que la vuelve terminal.
            outcomes.Add(RecipientHandoffOutcome.Rejected(
                recipient.RecipientId,
                result.Reason ?? CampaignNotification.ReasonAddressInvalid));
        }

        _logger.LogInformation(
            "notification-service aceptó el lote {BatchId} de la campaña {CampaignId} con {Count} destinatarios.",
            batch.BatchId,
            request.CampaignId,
            outcomes.Count);

        return Result.Success<SendBatchResult, BaseError>(new SendBatchResult(outcomes));
    }

    /// <summary>No contestó, o contestó algo que no es un 202. El lote se da por no entregado.</summary>
    private static Result<SendBatchResult, BaseError> Failure(string detail) =>
        Result.Failure<SendBatchResult, BaseError>(new NotificationsUnavailableError(detail));

    /// <summary>
    /// Contestó un 202 que no se puede emparejar. Mismo desenlace que una caída y código distinto,
    /// porque quien lea el log tiene que poder separar "estaba caído" de "contestó cualquier cosa".
    /// </summary>
    private static Result<SendBatchResult, BaseError> Unreadable(string detail) =>
        Result.Failure<SendBatchResult, BaseError>(new NotificationBatchResponseInvalidError(detail));
}

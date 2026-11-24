using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Core.Templates;

namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// Manda el correo al ganador de un sorteo de premio por notification-service (spec 03 §5).
///
/// <para>
/// <b>Nada de acá lanza</b>: la plantilla ausente, el POST
/// rechazado o la cancelación terminan en un <see cref="QuoteNotificationOutcome"/> y en el log.
/// </para>
///
/// <para>
/// <b>Es un notificador aparte y no un quinto momento de <see cref="QuoteNotifier"/>.</b> Comparten
/// el cliente, el repositorio de plantillas y la configuración, pero eligen destinatario e idioma de
/// formas que no tienen nada en común: acá es la copia congelada del sorteo y el idioma del evento,
/// sin directorio que consultar.
/// </para>
/// </summary>
public sealed class RaffleWinnerNotifier : IRaffleWinnerNotifier
{
    /// <summary>El tope de nombre por persona que cobra notification-service.</summary>
    private const int RecipientNameMaxLength = 200;

    /// <summary>Nombre de este servicio en el campo <c>about</c> del historial.</summary>
    private const string SourceService = "events";

    /// <summary>Nombre por omisión cuando no hay marca personalizada ni global configurada.</summary>
    private const string DefaultOrganizerName = "Sorteo360";

    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationClient _client;
    private readonly NotificationSettings _notificationSettings;
    private readonly ILogger<RaffleWinnerNotifier> _logger;
    private readonly IBrandingRepository? _branding;

    public RaffleWinnerNotifier(
        INotificationTemplateRepository templates,
        INotificationClient client,
        IOptions<NotificationSettings> notificationSettings,
        ILogger<RaffleWinnerNotifier> logger,
        IBrandingRepository? branding = null)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(notificationSettings);
        ArgumentNullException.ThrowIfNull(logger);

        _templates = templates;
        _client = client;
        _notificationSettings = notificationSettings.Value;
        _logger = logger;
        _branding = branding;
    }

    /// <inheritdoc />
    public async Task<QuoteNotificationOutcome> NotifyWonAsync(
        Event @event,
        Raffle raffle,
        Prize prize,
        PrizeDraw draw,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(raffle);
        ArgumentNullException.ThrowIfNull(prize);
        ArgumentNullException.ThrowIfNull(draw);

        try
        {
            // Defensivo: spec 01 §6 deja fuera del bombo a quien no tiene correo, así que un sorteo
            // vivo siempre lo trae.
            // Quien llegó sin correo tiene uno interno (clave del padrón): no se le escribe.
            if (string.IsNullOrWhiteSpace(draw.WinnerEmail) || RaffleParticipant.IsSyntheticEmail(draw.WinnerEmail))
                return QuoteNotificationOutcome.NoRecipient();

            var template = await FindTemplateAsync(@event.Language, cancellationToken);

            if (template is null)
            {
                _logger.LogError(
                    "No está sembrada la plantilla '{TemplateKey}' ni en '{Language}' ni en español; el ganador del sorteo {DrawId} no recibe correo.",
                    RaffleNotificationType.PrizeWon, @event.Language, draw.Id);

                return QuoteNotificationOutcome.Failed();
            }

            var organizerName = await ResolveOrganizerAsync(@event.Id, cancellationToken);
            var request = BuildRequest(@event, raffle, prize, draw, template, organizerName);

            var response = await _client.SendAsync(request, cancellationToken);

            if (response is null)
            {
                _logger.LogError(
                    "notification-service aceptó el correo al ganador del sorteo {DrawId} sin devolver el lote.",
                    draw.Id);

                return QuoteNotificationOutcome.Failed();
            }

            return QuoteNotificationOutcome.Sent(response.BatchId.ToString());
        }
        catch (Exception exception)
        {
            // Ancho a propósito, cancelación incluida: a esta altura el sorteo ya está completado, y
            // el porqué entero está en el catch de QuoteNotifier.NotifyAsync.
            _logger.LogError(
                exception,
                "No se pudo mandar el correo al ganador del sorteo {DrawId} (sorteo {RaffleId}).",
                draw.Id, raffle.Id);

            return QuoteNotificationOutcome.Failed();
        }
    }

    /// <summary>
    /// La copia en el idioma del evento y, si no la hay, en español (spec 03 §5.4). El respaldo no
    /// puede ser un rechazo: completar no falla por un correo, y mandar en español es mejor que no
    /// mandar nada.
    /// </summary>
    private async Task<NotificationTemplate?> FindTemplateAsync(string language, CancellationToken cancellationToken)
    {
        var template = await _templates.FindAsync(RaffleNotificationType.PrizeWon, language, cancellationToken);

        if (template is not null || language == AllowedLanguage.Espanol)
            return template;

        return await _templates.FindAsync(RaffleNotificationType.PrizeWon, AllowedLanguage.Espanol, cancellationToken);
    }

    /// <summary>
    /// Un lote por sorteo, con una sola persona: la copia del ganador que congeló el sorteo y no una
    /// consulta al CRM (spec 03 §5.2). Lleva el idioma de la plantilla que se usó, así que el
    /// respaldo al español queda a la vista en el historial.
    /// </summary>
    private SendNotificationRequest BuildRequest(
        Event @event,
        Raffle raffle,
        Prize prize,
        PrizeDraw draw,
        NotificationTemplate template,
        string organizerName)
    {
        return new SendNotificationRequest
        {
            Type = RaffleNotificationType.PrizeWon,
            Language = template.Language,
            FromAddress = _notificationSettings.FromAddress,
            Subject = template.Subject,
            Body = template.BodyHtml,
            SharedData = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nombre"] = draw.WinnerName,
                ["premio"] = prize.Name,
                ["evento"] = @event.Name,
                ["fechas"] = DatesOf(@event, template.Language),
                ["codigo"] = draw.RedemptionCode,
                ["organizador"] = organizerName,

                // Completar las exige, así que acá no pueden faltar. El vacío defensivo es para que
                // un marcador sin valor no llegue escrito como "{{instrucciones}}".
                ["instrucciones"] = raffle.PickupInstructions ?? string.Empty,
            },
            Recipients =
            [
                new NotificationRecipientRequest
                {
                    Email = draw.WinnerEmail,
                    Name = AsRecipientName(draw.WinnerName),
                },
            ],

            // El evento y el sorteo, nunca el sorteo de premio: nadie pregunta por el correo de un
            // intento. El puntero a este envío vive en la fila del sorteo de premio.
            About =
            [
                new NotificationAboutRequest
                {
                    SourceService = SourceService,
                    EntityType = "event",
                    EntityId = @event.Id.ToString(),
                },
                new NotificationAboutRequest
                {
                    SourceService = SourceService,
                    EntityType = "raffle",
                    EntityId = raffle.Id.ToString(),
                },
            ],

            IdempotencyKey = IdempotencyKeyFor(raffle.Id, draw.Id),
        };
    }

    private async Task<string> ResolveOrganizerAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (_branding is null)
            return DefaultOrganizerName;

        var eventBranding = await _branding.GetEventBrandingAsync(eventId, cancellationToken);
        if (eventBranding.IsSuccess && !string.IsNullOrWhiteSpace(eventBranding.Value?.CompanyName))
            return eventBranding.Value.CompanyName.Trim();

        var globalBranding = await _branding.GetGlobalAsync(cancellationToken);
        if (globalBranding.IsSuccess && !string.IsNullOrWhiteSpace(globalBranding.Value.OrganizationName))
            return globalBranding.Value.OrganizationName.Trim();

        return DefaultOrganizerName;
    }

    /// <summary>
    /// Las fechas del evento en el idioma de la plantilla, porque el dialecto no formatea nada
    /// (spec 03 §5.4): una sola si empieza y termina el mismo día, un rango si no. Sale del patrón
    /// largo de la cultura, así que una plantilla en otro idioma no pide código nuevo.
    /// </summary>
    internal static string DatesOf(Event @event, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        var start = @event.StartDate.ToString("D", culture);

        return @event.StartDate == @event.EndDate
            ? start
            : $"{start} – {@event.EndDate.ToString("D", culture)}";
    }

    /// <summary>
    /// <c>events:raffle:{raffleId}:draw:{drawId}:won</c>. Por sorteo y nunca aleatoria: un reintento
    /// del mismo completar se reproduce, y reemitir no tiene camino hasta acá.
    /// </summary>
    private static string IdempotencyKeyFor(Guid raffleId, Guid drawId) =>
        $"events:raffle:{raffleId}:draw:{drawId}:won";

    /// <summary>El nombre para la cabecera To, cortado al tope que cobra notification-service.</summary>
    private static string AsRecipientName(string fullName) =>
        fullName.Length <= RecipientNameMaxLength ? fullName : fullName[..RecipientNameMaxLength];
}

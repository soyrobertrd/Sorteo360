using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Notifications;

public sealed class AccountOnboardingNotifier : IAccountOnboardingNotifier
{
    private readonly INotificationClient _notifications;
    private readonly NotificationSettings _notificationSettings;
    private readonly AppSettings _appSettings;
    private readonly ILogger<AccountOnboardingNotifier> _logger;

    public AccountOnboardingNotifier(
        INotificationClient notifications,
        IOptions<NotificationSettings> notificationSettings,
        IOptions<AppSettings> appSettings,
        ILogger<AccountOnboardingNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(notificationSettings);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(logger);
        _notifications = notifications;
        _notificationSettings = notificationSettings.Value;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    public async Task SendActivationEmailAsync(
        string email,
        string fullName,
        string companyName,
        string rawToken,
        CancellationToken cancellationToken)
    {
        var baseUrl = (_appSettings.BaseUrl ?? string.Empty).TrimEnd('/');
        var link = $"{baseUrl}/activar/{Uri.EscapeDataString(rawToken)}";

        try
        {
            var payload = new SendNotificationRequest
            {
                Type = "events.account.activation",
                Language = "es",
                FromAddress = _notificationSettings.FromAddress,
                FromName = "Sorteo360",
                // Nombre y empresa los escribe quien se registra: viajan como valores y
                // notification-service los escapa al sustituirlos en el cuerpo HTML.
                Subject = "Activa tu cuenta de {{empresa}} — Sorteo360",
                Body = "Hola {{nombre}},\n\n"
                    + "Gracias por registrar {{empresa}} en Sorteo360.\n"
                    + "Activa tu cuenta y define tu contraseña aquí (válido por 24 horas):\n{{enlace}}\n\n"
                    + "Sorteo360",
                SharedData = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["nombre"] = fullName,
                    ["empresa"] = companyName,
                    ["enlace"] = link,
                },
                Recipients = [new NotificationRecipientRequest { Email = email, Name = fullName }],
                About = [new NotificationAboutRequest { SourceService = "events-service", EntityType = "account_activation", EntityId = email }],
                IdempotencyKey = $"account-activation-{email}-{DateTimeOffset.UtcNow:yyyyMMddHHmm}",
            };

            await _notifications.SendAsync(payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo enviar el correo de activación a {Email}", email);
        }
    }
}


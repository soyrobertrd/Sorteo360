using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Infrastructure.Smtp;

/// <inheritdoc cref="ISmtpSessionFactory" />
internal sealed class MailKitSmtpSessionFactory : ISmtpSessionFactory
{
    private readonly IOptions<SmtpSettings> _settings;
    private readonly ILogger<MailKitSmtpSessionFactory> _logger;

    public MailKitSmtpSessionFactory(IOptions<SmtpSettings> settings, ILogger<MailKitSmtpSessionFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ISmtpSession> OpenAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Value;
        var client = new SmtpClient();

        try
        {
            // Dos opciones y nunca una tercera. StartTls falla cerrado, que es lo que se quiere en
            // producción: la documentación del proveedor calla sobre si rechaza AUTH en una sesión
            // en claro, y el silencio no es una garantía. None es lo que sirve Mailpit.
            var security = settings.RequireTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);

            if (settings.HasCredentials)
                await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);

            _logger.LogDebug(
                "Sesión SMTP abierta contra {Host}:{Port} ({Security})",
                settings.Host,
                settings.Port,
                security);

            return new MailKitSmtpSession(client);
        }
        catch (Exception ex)
        {
            client.Dispose();

            // Conectar y autenticar son parte del envío: si fallan, fallan como falla un envío, con
            // el código que corresponda para que el worker sepa si reintentar.
            throw ex switch
            {
                SmtpCommandException command => new SmtpDeliveryException((int)command.StatusCode, command.Message, ex),
                AuthenticationException => new SmtpDeliveryException(SmtpFailureClassifier.AuthenticationFailed, ex.Message, ex),
                SmtpDeliveryException => ex,
                _ => new SmtpDeliveryException(null, ex.Message, ex)
            };
        }
    }
}

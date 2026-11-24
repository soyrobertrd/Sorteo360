using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Infrastructure.Smtp;

/// <summary>
/// La pata SMTP de verdad, sobre MailKit. Es la única que existe: este mismo código corre contra
/// Mailpit en local y en stage y contra el proveedor en producción, y lo único que cambia entre los
/// dos es si la sesión exige STARTTLS.
/// </summary>
internal sealed class MailKitSmtpSession : ISmtpSession
{
    private readonly SmtpClient _client;

    public MailKitSmtpSession(SmtpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    /// <inheritdoc />
    public async Task<string> SendAsync(OutboundEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var message = Build(email);

        try
        {
            // La respuesta del servidor es el "250 Ok: queued as <token>" que se guarda contra la
            // fila. Es lo primero que pide el soporte del proveedor.
            return await _client.SendAsync(message, cancellationToken);
        }
        catch (SmtpCommandException ex)
        {
            // El servidor contestó con un código: es él quien dice si esto se reintenta o no.
            throw new SmtpDeliveryException((int)ex.StatusCode, ex.Message, ex);
        }
        catch (AuthenticationException ex)
        {
            throw new SmtpDeliveryException(SmtpFailureClassifier.AuthenticationFailed, ex.Message, ex);
        }
        catch (Exception ex) when (ex is SmtpProtocolException or SslHandshakeException or SocketException or IOException)
        {
            // No llegó a haber conversación, así que no hay código que clasificar y el fallo es
            // transitorio por defecto.
            throw new SmtpDeliveryException(null, ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync(quit: true, CancellationToken.None);

        _client.Dispose();
    }

    /// <summary>
    /// Arma el mensaje: una persona en <c>To</c>, sin <c>Cc</c> y sin <c>Bcc</c>, y las dos partes
    /// MIME en un <c>multipart/alternative</c>.
    /// </summary>
    private static MimeMessage Build(OutboundEmail email)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(email.FromName ?? string.Empty, email.FromAddress));
        // "María González <maria@…>" cuando quien llama mandó el nombre; la dirección sola si no.
        message.To.Add(new MailboxAddress(email.ToName ?? string.Empty, email.ToAddress));
        message.Subject = email.Subject;

        // Con las dos partes puestas, BodyBuilder produce multipart/alternative. La de texto se
        // derivó del HTML ya sustituido: no hay una segunda copia que mantener.
        var body = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody
        };

        message.Body = body.ToMessageBody();

        // La forma de las dos cabeceras es de OutboundHeaders, no de acá: el doble de las pruebas
        // usa la misma, así que no puede pasar mientras esto manda otra cosa.
        message.Headers.Add(OutboundHeaders.SmtpApi, OutboundHeaders.UniqueArgs(email.RecipientId));

        if (email.UnsubscribeUrl is not null)
            message.Headers.Add(OutboundHeaders.ListUnsubscribe, OutboundHeaders.UnsubscribeValue(email.UnsubscribeUrl));

        return message;
    }

}

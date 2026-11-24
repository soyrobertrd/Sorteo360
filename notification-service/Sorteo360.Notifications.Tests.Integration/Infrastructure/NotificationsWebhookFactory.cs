using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sorteo360.Notifications.Core.Sending;
using Sorteo360.Notifications.Web.WebhookEndpoints;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// La API contra un PostgreSQL de verdad, con una clave pública de webhook configurada y generada
/// para esta corrida. Es el fixture del ticket del webhook.
/// </summary>
public sealed class NotificationsWebhookFactory : NotificationsApiDbFactory
{
    /// <summary>La clave con la que este fixture firma. La privada nunca sale de la memoria.</summary>
    public WebhookSigningKey Key { get; } = new();

    /// <summary>
    /// La pata SMTP simulada. Está acá porque una de las cosas que este ticket tiene que probar es
    /// el ciclo entero: una baja llega por el webhook, retira la dirección, y el siguiente envío a
    /// esa dirección sale como fila <c>dropped</c> en vez de no salir.
    /// </summary>
    public FakeSmtpSessionFactory Smtp { get; } = new();

    /// <summary>Un drenador nuevo, con su propio ámbito y su propio <c>DbContext</c>.</summary>
    public async Task<int> DrainAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var drain = scope.ServiceProvider.GetRequiredService<INotificationDrainService>();

        return await drain.DrainAsync(CancellationToken.None);
    }

    /// <summary>
    /// Una petición al webhook con el cuerpo <b>tal cual</b> y su firma.
    /// <para>
    /// El cuerpo se escribe como bytes UTF-8 explícitos y no como objeto: la firma cubre los bytes,
    /// así que dejar que algo los vuelva a serializar en el camino es exactamente el fallo que
    /// estos tests existen para descartar.
    /// </para>
    /// </summary>
    /// <param name="rawBody">El cuerpo a mandar.</param>
    /// <param name="signedBody">
    /// El cuerpo que se firma, cuando se quiere probar que difiere del que se manda. Por omisión es
    /// el mismo.
    /// </param>
    /// <param name="signedAt">La marca de tiempo a firmar. Por omisión, ahora.</param>
    /// <param name="signature">Una firma puesta a mano, para los casos de firma inválida.</param>
    public HttpRequestMessage SignedRequest(
        string rawBody,
        string? signedBody = null,
        DateTimeOffset? signedAt = null,
        string? signature = null)
    {
        var timestamp = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        var request = new HttpRequestMessage(HttpMethod.Post, SendGridEventWebhookEndpoint.Route)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(rawBody))
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add(SendGridEventWebhookEndpoint.TimestampHeader, timestamp);
        request.Headers.Add(
            SendGridEventWebhookEndpoint.SignatureHeader,
            signature ?? Key.Sign(timestamp, signedBody ?? rawBody));

        // El proveedor no manda token. Sin esta cabecera el doble de autenticación de los tests
        // autenticaría la petición sola, y el test dejaría de probar que la ruta es anónima.
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        return request;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("SendGrid:EventWebhook:PublicKey", Key.PublicKeyBase64);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmtpSessionFactory>();
            services.AddSingleton<ISmtpSessionFactory>(Smtp);
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Key.Dispose();

        base.Dispose(disposing);
    }
}

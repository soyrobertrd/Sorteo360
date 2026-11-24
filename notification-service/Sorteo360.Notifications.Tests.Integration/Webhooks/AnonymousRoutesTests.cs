using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Sorteo360.Notifications.Web.WebhookEndpoints;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Webhooks;

/// <summary>
/// El webhook es <b>la única</b> ruta anónima no sanitaria del servicio, y este test es lo que lo
/// mantiene así.
/// <para>
/// Se recorre la tabla de rutas registrada y no una lista escrita a mano: así un
/// <c>.AllowAnonymous()</c> que alguien agregue mañana rompe este test en vez de pasar
/// desapercibido. Es la propiedad que más cuesta si algún día deja de ser cierta —lo que hay detrás
/// de la fallback policy es todo el correo que el MITUR haya mandado nunca.
/// </para>
/// </summary>
public sealed class AnonymousRoutesTests : IClassFixture<NotificationsApiFactory>
{
    /// <summary>
    /// Los prefijos de lo sanitario y de la documentación: lo anónimo que ya existía antes de este
    /// ticket. Se comparan por primer segmento y no por ruta exacta porque Scalar monta sus propios
    /// recursos (su script, su favicon) y una lista literal se rompería en la próxima versión suya,
    /// que es justo el ruido que haría que alguien apagara este test.
    /// </summary>
    private static readonly string[] HealthAndDocsPrefixes =
    [
        "_alive",
        "_ready",
        "_health",
        "openapi",
        "scalar"
    ];

    private readonly NotificationsApiFactory _factory;

    public AnonymousRoutesTests(NotificationsApiFactory factory) => _factory = factory;

    [Fact]
    public void AnonymousRoutes_ReturnsOnlyTheWebhook_WhenHealthRoutesAreExcluded()
    {
        var anonymous = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Select(route => route.TrimStart('/'))
            .Where(route => !HealthAndDocsPrefixes.Contains(
                route.Split('/', 2)[0], StringComparer.Ordinal))
            .ToArray();

        anonymous.ShouldBe([SendGridEventWebhookEndpoint.Route.TrimStart('/')]);
    }

    [Fact]
    public void EventWebhook_ReturnsNoKeycloakPolicy_WhenItsMetadataIsRead()
    {
        // La quinta cosa de la revisión de seguridad. El proveedor no es un usuario del realm y no
        // hay token que pueda traer; lo que autentica el lote es la firma.
        var endpoint = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(route => route.RoutePattern.RawText == SendGridEventWebhookEndpoint.Route);

        endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldNotBeNull();
        endpoint.Metadata.GetMetadata<IAuthorizeData>().ShouldBeNull();
    }

    [Fact]
    public async Task EventWebhook_Returns403_WhenNoPublicKeyIsConfigured()
    {
        // Falla cerrado. El fallo que de verdad va a ocurrir es un despliegue a medias, y aceptar
        // el lote entonces abriría el endpoint a cualquiera. Esta factory no configura clave.
        var request = new HttpRequestMessage(HttpMethod.Post, SendGridEventWebhookEndpoint.Route)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        };

        request.Headers.Add(SendGridEventWebhookEndpoint.TimestampHeader,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add(SendGridEventWebhookEndpoint.SignatureHeader, Convert.ToBase64String([1, 2, 3]));
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendAndHistoryRoutes_Returns401_WhenCalledAnonymously()
    {
        // Lo que Traefik publica es el host del webhook con esta ruta como prefijo; estas otras no
        // son alcanzables desde internet. Que además contesten 401 es el cinturón sobre el tirante.
        foreach (var route in new[] { "/v1/notifications", "/v1/notifications/orphan-events" })
        {
            var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

            var response = await _factory.CreateClient().SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"La ruta {route} dejó entrar a un anónimo.");
        }
    }
}

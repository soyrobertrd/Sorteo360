using System.Net;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.HealthEndpoints;

/// <summary>
/// Los endpoints de salud del chasis, servidos por este host. El <c>HEALTHCHECK</c> de la
/// imagen y el <c>depends_on</c> de Compose dependen de <c>/_alive</c>, y por eso sigue
/// siendo anónimo: un servicio que autenticara su propio health check nunca reportaría sano.
/// </summary>
public sealed class HealthEndpointsTests : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory _factory;

    public HealthEndpointsTests(NotificationsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Alive_Returns200_WhenCalledWithoutAToken()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/_alive");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_Returns200_WhenTheServiceCanTakeTraffic()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

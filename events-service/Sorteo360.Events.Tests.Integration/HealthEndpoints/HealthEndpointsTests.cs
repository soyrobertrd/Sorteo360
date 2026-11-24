using System.Net;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.HealthEndpoints;

/// <summary>
/// Smoke tests del cableado de los endpoints de salud: verifican que la API arranca
/// y que liveness/readiness/reporte responden correctamente.
/// </summary>
public sealed class HealthEndpointsTests : IClassFixture<EventsApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(EventsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Alive_ReturnsOk_WhenTheServiceIsUp()
    {
        var response = await _client.GetAsync("/_alive");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_ReturnsOk_WhenDependenciesAreHealthy()
    {
        var response = await _client.GetAsync("/_ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthAll_ReturnsOk_WhenDependenciesAreHealthy()
    {
        var response = await _client.GetAsync("/_health/all");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

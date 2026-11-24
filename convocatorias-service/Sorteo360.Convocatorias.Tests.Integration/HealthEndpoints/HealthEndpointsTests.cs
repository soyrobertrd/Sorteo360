using System.Net;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.HealthEndpoints;

/// <summary>
/// Los endpoints de salud del chasis, servidos por este host. El <c>HEALTHCHECK</c> de la
/// imagen y el <c>depends_on</c> de Compose dependen de <c>/_alive</c>.
/// </summary>
public sealed class HealthEndpointsTests : IClassFixture<ConvocatoriasApiFactory>
{
    private readonly ConvocatoriasApiFactory _factory;

    public HealthEndpointsTests(ConvocatoriasApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Alive_Returns200_WhenTheProcessIsUp()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/_alive");

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

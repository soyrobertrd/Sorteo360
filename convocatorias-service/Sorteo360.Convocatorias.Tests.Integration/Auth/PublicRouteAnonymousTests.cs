using System.Net;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Auth;

/// <summary>
/// El prefijo público es <c>AllowAnonymous</c>, y la fallback nunca lo gobernó: el portal de
/// registro sigue respondiendo sin token, pida lo que pida la fallback (ADR-0025). Va con la
/// factory del portal porque la lista pública lee la base y los eventos.
/// </summary>
public sealed class PublicRouteAnonymousTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public PublicRouteAnonymousTests(RegistrationPortalFactory factory) => _factory = factory;

    [Fact]
    public async Task ListPublicEvents_Returns200_WhenCalledWithoutAToken()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/public/events");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

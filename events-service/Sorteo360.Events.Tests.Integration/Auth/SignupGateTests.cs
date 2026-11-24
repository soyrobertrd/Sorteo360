using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Auth;

/// <summary>
/// S1.1 (auditoría 2026-11): el registro anónimo da <c>account_admin</c>, así que con
/// <c>Accounts:SignupEnabled</c> apagado (el valor por omisión) sus rutas no existen.
/// </summary>
public sealed class SignupGateTests : IClassFixture<EventsApiFactory>
{
    private static readonly string[] SignupRoutes =
    [
        "v{version:apiVersion}/accounts/activate",
        "v{version:apiVersion}/accounts/signup",
        "v{version:apiVersion}/accounts/signup/resend",
    ];

    private readonly EventsApiFactory _factory;

    public SignupGateTests(EventsApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/v1/accounts/signup")]
    [InlineData("/v1/accounts/signup/resend")]
    [InlineData("/v1/accounts/activate")]
    public async Task Post_Returns404_WhenSignupIsDisabled(string path)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        using var response = await client.PostAsJsonAsync(path, new { email = "a@b.do", token = "x", password = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void MapEndpoints_MapsSignupRoutes_WhenSignupIsEnabled()
    {
        // La cadena de conexión va también por UseSetting: la variable de entorno de la factory es de
        // todo el proceso y otra clase en paralelo puede borrarla al hacer Dispose.
        using var enabled = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Accounts:SignupEnabled", "true")
            .UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=test;Username=test;Password=test"));

        var routes = enabled.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var route in SignupRoutes)
            routes.ShouldContain(route);
        routes.ShouldNotContain("v{version:apiVersion}/accounts/{**path}");
    }
}

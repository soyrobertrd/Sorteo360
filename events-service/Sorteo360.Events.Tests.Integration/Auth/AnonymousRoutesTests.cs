using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Auth;

/// <summary>Comprueba que las únicas rutas anónimas, fuera de la salud y la documentación, son las rutas públicas explícitas del portal/webhooks.</summary>
public sealed class AnonymousRoutesTests : IClassFixture<EventsApiFactory>
{
    /// <summary>Los primeros segmentos de las rutas de salud y de documentación, que son anónimas desde antes.</summary>
    /// <remarks>Se compara el primer segmento porque Scalar monta sus propios recursos, y una lista literal se rompería con su próxima versión.</remarks>
    private static readonly string[] HealthAndDocsPrefixes = ["_alive", "_ready", "_health", "openapi", "scalar"];

    private readonly EventsApiFactory _factory;

    public AnonymousRoutesTests(EventsApiFactory factory) => _factory = factory;

    [Fact]
    public void MapEndpoints_ReturnsOnlyPublicAndWebhookRoutesAsAnonymous_WhenHealthAndDocsAreLeftOut()
    {
        var anonymous = AnonymousEndpoints()
            .Select(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .Where(route => !HealthAndDocsPrefixes.Contains(route.Split('/', 2)[0], StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Sin Accounts:SignupEnabled el registro de cuentas es un 404 fijo (SignupGateTests).
        anonymous.ShouldBe([
            "v{version:apiVersion}/accounts/{**path}",
            "v{version:apiVersion}/branding/",
            "v{version:apiVersion}/events/{eventId:guid}/raffle/webhooks/check-in",
            "v{version:apiVersion}/events/{eventId:guid}/raffle/webhooks/participants",
            "v{version:apiVersion}/feature-switches/",
            "v{version:apiVersion}/public/feature-switches/",
        ]);
    }

    /// <summary>Lista los endpoints registrados que llevan <c>AllowAnonymous</c>.</summary>
    private IEnumerable<RouteEndpoint> AnonymousEndpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
}

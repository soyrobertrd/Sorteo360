using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Platform.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.GlobalEndpoints;

/// <summary>
/// Descubrimiento de endpoints: el chasis siempre se recorre a sí mismo, de modo que
/// un servicio que solo pasa su propio ensamblado conserva igual las rutas de salud
/// (de las que dependen el HEALTHCHECK de la imagen y el depends_on de Compose), y un
/// ensamblado repetido se recorre una sola vez (recorrerlo dos veces duplicaría lo que
/// registra <see cref="IEndpoint.AddServices"/>, sin ninguna señal visible desde afuera).
/// </summary>
public sealed class EndpointExtensionsTests
{
    private static readonly Assembly ChassisAssembly = typeof(EndpointExtensions).Assembly;

    private static readonly Assembly CallerAssembly = typeof(EndpointExtensionsTests).Assembly;

    /// <summary>
    /// Un ensamblado sin ninguna implementación de <see cref="IEndpoint"/>: representa al
    /// servicio que adopta el chasis y solo pasa su propio ensamblado.
    /// </summary>
    private static readonly Assembly AssemblyWithoutEndpoints = typeof(FactAttribute).Assembly;

    private static readonly string[] HealthRoutes = ["/_alive", "/_ready", "/_health", "/_health/all"];

    /// <summary>Ningún endpoint bajo prueba lee configuración; alcanza con una vacía.</summary>
    private static readonly IConfiguration EmptyConfiguration = new ConfigurationBuilder().Build();

    /// <summary>
    /// Host mínimo con los health checks registrados: <c>MapHealthChecks</c> exige que
    /// <c>HealthCheckService</c> esté en el contenedor al mapear la ruta.
    /// </summary>
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHealthChecks();
        return builder.Build();
    }

    /// <summary>Patrones de ruta ya mapeados sobre el host, con repeticiones incluidas.</summary>
    private static List<string> MappedRoutes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

    /// <summary>Cantidad de registros del marcador que deja <see cref="MarkerEndpoint"/>.</summary>
    private static int MarkerRegistrations(IServiceCollection services) =>
        services.Count(descriptor => descriptor.ServiceType == typeof(ScanMarker));

    #region El chasis se recorre a sí mismo

    [Fact]
    public void MapEndpoints_MapsTheHealthRoutes_WhenTheCallerAssemblyHasNoEndpoint()
    {
        var app = CreateApp();

        app.MapEndpoints(AssemblyWithoutEndpoints);

        MappedRoutes(app).ShouldBe(HealthRoutes, ignoreOrder: true);
    }

    [Fact]
    public void MapEndpoints_MapsTheHealthRoutes_WhenNoAssemblyIsPassed()
    {
        var app = CreateApp();

        app.MapEndpoints();

        MappedRoutes(app).ShouldBe(HealthRoutes, ignoreOrder: true);
    }

    [Fact]
    public void MapEndpoints_MapsTheCallerEndpointsBesidesTheHealthRoutes_WhenGivenTheCallerAssembly()
    {
        var app = CreateApp();

        app.MapEndpoints(CallerAssembly);

        MappedRoutes(app).ShouldBe([.. HealthRoutes, MarkerEndpoint.Route], ignoreOrder: true);
    }

    [Fact]
    public void AddEndpoints_InvokesAddServicesOnTheCallerEndpoints_WhenGivenTheCallerAssembly()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(EmptyConfiguration, CallerAssembly);

        MarkerRegistrations(services).ShouldBe(1);
    }

    #endregion

    #region Un ensamblado repetido se recorre una sola vez

    [Fact]
    public void MapEndpoints_MapsTheHealthRoutesOnce_WhenTheChassisAssemblyIsPassedExplicitly()
    {
        var app = CreateApp();

        app.MapEndpoints(ChassisAssembly);

        MappedRoutes(app).ShouldBe(HealthRoutes, ignoreOrder: true);
    }

    [Fact]
    public void MapEndpoints_MapsTheHealthRoutesOnce_WhenTheCallerAlsoPassesTheChassisAssembly()
    {
        var app = CreateApp();

        app.MapEndpoints(CallerAssembly, ChassisAssembly);

        MappedRoutes(app).ShouldBe([.. HealthRoutes, MarkerEndpoint.Route], ignoreOrder: true);
    }

    [Fact]
    public void MapEndpoints_MapsTheCallerEndpointsOnce_WhenTheSameAssemblyIsPassedTwice()
    {
        var app = CreateApp();

        app.MapEndpoints(CallerAssembly, CallerAssembly);

        MappedRoutes(app).ShouldBe([.. HealthRoutes, MarkerEndpoint.Route], ignoreOrder: true);
    }

    [Fact]
    public void AddEndpoints_InvokesAddServicesOnce_WhenTheSameAssemblyIsPassedTwice()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(EmptyConfiguration, CallerAssembly, CallerAssembly);

        MarkerRegistrations(services).ShouldBe(1);
    }

    /// <summary>
    /// Forma exacta de la llamada de un servicio que pasa el chasis además del suyo. Solo
    /// cubre el lado de quien llama: <c>HealthEndpoints.AddServices</c> no registra nada,
    /// así que una pasada de más sobre el chasis no deja rastro en el contenedor — eso lo
    /// detecta <see cref="MapEndpoints_MapsTheHealthRoutesOnce_WhenTheChassisAssemblyIsPassedExplicitly"/>
    /// contando rutas.
    /// </summary>
    [Fact]
    public void AddEndpoints_InvokesCallerAddServicesOnce_WhenTheChassisAssemblyIsAlsoPassed()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(EmptyConfiguration, CallerAssembly, ChassisAssembly);

        MarkerRegistrations(services).ShouldBe(1);
    }

    #endregion
}

/// <summary>Marcador que deja <see cref="MarkerEndpoint.AddServices"/> por cada pasada.</summary>
internal sealed class ScanMarker;

/// <summary>
/// Endpoint de prueba del ensamblado que llama al descubrimiento: deja rastro tanto en el
/// contenedor como en la tabla de rutas, así una pasada de más se vuelve observable.
/// </summary>
internal sealed class MarkerEndpoint : IEndpoint
{
    public const string Route = "/_scan-marker";

    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<ScanMarker>();

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app) =>
        app.MapGet(Route, () => Results.Ok());
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Routing;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Auth;

/// <summary>
/// El reparto entre lo público y lo interno, comprobado en proceso.
/// <para>
/// Los endpoints se descubren por reflexión y cada uno mapea sus propias rutas sin
/// estructura que lo obligue a nada, así que "lo público vive aparte" es una convención que
/// un <c>AllowAnonymous</c> mal puesto rompe en silencio. Este test la vuelve mecánica:
/// todo endpoint mapeado o cae bajo el prefijo público, o está en la lista de salud, o
/// exige autorización.
/// </para>
/// </summary>
public sealed class PublicSurfaceGuardTests : IClassFixture<ConvocatoriasApiFactory>
{
    /// <summary>
    /// Las únicas rutas anónimas fuera del prefijo público, enumeradas una por una.
    /// <para>
    /// Las de salud porque el <c>HEALTHCHECK</c> de la imagen y el <c>depends_on</c> de
    /// Compose dependen de que sigan abiertas. Las de documentación porque se navegan desde
    /// un navegador, que no lleva token, igual que en los servicios hermanos; lo que las
    /// mantiene fuera de internet es la red y no un rol, porque el nginx del frontend
    /// reenvía los dos prefijos de la API y ninguna otra ruta.
    /// </para>
    /// <para>
    /// La lista es de rutas exactas: cualquier ruta anónima nueva, incluida una que traiga
    /// una versión distinta de Scalar, aparece acá como fallo y se decide a mano.
    /// </para>
    /// </summary>
    private static readonly string[] AnonymousByDesign =
    [
        "_alive",
        "_ready",
        "_health",
        "_health/all",
        "openapi/{documentName}.json",
        "scalar/{documentName?}",
        "scalar/scalar.js",
        "scalar/scalar.aspnetcore.js",
        "scalar/favicon.svg",
    ];

    private readonly ConvocatoriasApiFactory _factory;

    public PublicSurfaceGuardTests(ConvocatoriasApiFactory factory) => _factory = factory;

    [Fact]
    public void MappedEndpoints_ReturnsNoUnprotectedRoute_WhenScanned()
    {
        var endpoints = _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>();

        var unprotected = endpoints
            .Where(endpoint => !IsAnonymousByDesign(endpoint))
            .Where(endpoint => !SitsUnderPublicPrefix(endpoint))
            .Where(endpoint => !RequiresAuthorization(endpoint))
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        unprotected.ShouldBeEmpty(
            $"Endpoints sin protección fuera del prefijo público: {string.Join(", ", unprotected)}");
    }

    /// <summary>
    /// La ruta completa, no un prefijo suyo: con <c>StartsWith</c> un <c>_healthz-debug</c>
    /// quedaría exento sin que nadie lo decidiera.
    /// </summary>
    private static bool IsAnonymousByDesign(RouteEndpoint endpoint)
    {
        var pattern = (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
        return AnonymousByDesign.Contains(pattern, StringComparer.Ordinal);
    }

    private static bool SitsUnderPublicPrefix(RouteEndpoint endpoint) =>
        (endpoint.RoutePattern.RawText ?? string.Empty)
            .TrimStart('/')
            .StartsWith(ApiRoutes.PublicPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Un endpoint queda protegido por una política explícita o por la fallback policy. La
    /// fallback solo se aplica donde no hay <c>AllowAnonymous</c>, así que su presencia es
    /// exactamente lo que hay que buscar.
    /// </summary>
    private static bool RequiresAuthorization(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
}

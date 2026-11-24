using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Platform.HealthEndpoints;

/// <summary>
/// Expone los endpoints de salud: liveness (<c>/_alive</c>), readiness (<c>/_ready</c>)
/// y reportes detallados (<c>/_health</c>, <c>/_health/all</c>). Todos son anónimos.
/// </summary>
public sealed class HealthEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Los health checks los registra el host al configurar sus servicios de datos.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        // Liveness: el proceso está vivo. No evalúa dependencias externas.
        app.MapGet("/_alive", () => Results.Ok())
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Readiness: listo para recibir tráfico (dependencias críticas OK).
        app.MapHealthChecks("/_ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        }).AllowAnonymous();

        // Reporte detallado de los checks críticos / internos.
        app.MapHealthChecks("/_health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("critical") || check.Tags.Contains("internal"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        }).AllowAnonymous();

        // Reporte detallado de todos los checks registrados.
        app.MapHealthChecks("/_health/all", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        }).AllowAnonymous();
    }
}

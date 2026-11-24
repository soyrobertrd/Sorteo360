using System.Diagnostics.CodeAnalysis;
using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Sorteo360.Platform.Configuration;

/// <summary>
/// Registra los servicios transversales de la capa web que comparte todo servicio de
/// la plataforma: versionado de API, documentación (OpenAPI/Scalar) y logging (Serilog).
/// La autenticación queda fuera a propósito: no todos los servicios la tienen, así que
/// se añade aparte con <c>AddKeycloakAuthentication</c> y el que no la llama no registra
/// nada que no use.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WebServicesConfiguration
{
    /// <summary>
    /// Registra Serilog, el versionado de API y el documento OpenAPI de la versión v1.
    /// </summary>
    /// <param name="configuration">Configuración del host; de ahí se lee Serilog por completo.</param>
    /// <param name="host">Host builder al que se le engancha Serilog como proveedor de logging.</param>
    /// <param name="configureOpenApiDocument">
    /// Lo que el servicio aporta al documento: su document transformer (título y descripción
    /// que muestra Scalar) y los operation transformers que quiera. Es el punto de extensión
    /// por el que el chasis no necesita nombrar ningún tipo de un servicio.
    /// </param>
    public static IServiceCollection AddWebServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostBuilder host,
        Action<OpenApiOptions> configureOpenApiDocument)
    {
        // Configuración de Serilog: se lee por completo desde appsettings.json
        // (y sus overrides por entorno) mediante ReadFrom.Configuration.
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        // Reemplaza el proveedor de logging por defecto de Microsoft con Serilog.
        host.UseSerilog();

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        // Documento OpenAPI nativo de .NET 10 para la versión v1 de la API.
        // El nombre del documento ("v1") coincide con el GroupNameFormat del ApiExplorer,
        // por lo que solo se incluyen los endpoints de esa versión. Se sirve en
        // /openapi/v1.json y lo consume la UI de Scalar.
        services.AddOpenApi("v1", configureOpenApiDocument);

        return services;
    }
}

using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Configuration;
using Sorteo360.Convocatorias.Web.OpenApi;
using Sorteo360.Convocatorias.Web.RateLimiting;
using Sorteo360.Platform.Auth;
using Sorteo360.Platform.Configuration;
using Sorteo360.Platform.GlobalEndpoints;
using Sorteo360.Platform.Http;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// Codificaciones heredadas (Windows-1252 y compañía) para leer los listados que exporta
// un Excel en español. .NET en Linux solo trae las de Unicode, y sin este registro
// Encoding.GetEncoding(1252) tira NotSupportedException. Va antes de construir el host
// para que ninguna petición pueda llegar primero. No hace falta pin de paquete:
// System.Text.Encoding.CodePages viene en el framework compartido de .NET 10.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Remove the Server response header.
builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

// Register web services: API versioning, documentation and logging (Serilog).
// El transformer del documento OpenAPI es el de este servicio: fija el título y la
// descripción que muestra Scalar.
builder.Services.AddWebServices(builder.Configuration, builder.Host, options =>
    options.AddDocumentTransformer<ConvocatoriasDocumentTransformer>());

// Autenticación (JWT de Keycloak) y las políticas por rol funcional de este servicio,
// con la fallback policy que exige usuario autenticado en todo endpoint que no diga
// lo contrario.
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationPolicies();

// La IP real de quien llama, detrás de Traefik y del nginx del frontend.
builder.Services.AddProxyHeaders(proxyHopCount: 2);

// Límites de peticiones del prefijo público (el portal de registro).
builder.Services.AddPublicRateLimiting();

// Un JSON con la misma clave dos veces se rechaza: el limitador lee la primera "email" y el
// binder la última, así que repetirla esquivaba el límite por correo (auditoría run-1).
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.AllowDuplicateProperties = false);

// Register data services: PostgreSQL, in-memory cache and health checks.
builder.Services.AddDataServices<ConvocatoriasDbContext>(builder.Configuration);

// Register domain services: repositories and application services.
builder.Services.AddDomainServices(builder.Configuration);

// Auto-discover and register feature endpoints. El chasis se recorre solo, así que
// los endpoints de salud entran sin nombrarlos aquí.
builder.Services.AddEndpoints(builder.Configuration, Assembly.GetExecutingAssembly());

var app = builder.Build();

// Primero de la tubería: reescribe la IP y el esquema con lo que dicen los proxies, para
// que todo lo que venga después —el limitador, el log— vea al cliente y no al contenedor.
app.UseForwardedHeaders();

// Las migraciones de EF Core son la única autoridad del esquema: se aplican las
// pendientes en cada arranque, antes de aceptar tráfico. Si la base no está
// disponible el servicio falla rápido (Compose lo reintenta vía depends_on/healthcheck).
// EF serializa migraciones concurrentes con un lock a nivel de base de datos.
// En "IntegrationTesting" se omite: la factory con Testcontainers ejecuta las mismas
// migraciones contra su contenedor, y el smoke test corre sin base de datos real.
if (!app.Environment.IsEnvironment("IntegrationTesting"))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Logging de peticiones HTTP con Serilog: degrada el nivel de los health checks
// y eleva el nivel ante errores 4xx/5xx o excepciones.
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, _, ex) =>
    {
        var path = httpContext.Request.Path.Value;
        var statusCode = httpContext.Response.StatusCode;

        if (path is not null && (path.StartsWith("/_health") || path is "/_alive" or "/_ready") && statusCode == 200)
            return LogEventLevel.Debug;
        if ((path is "/_alive" or "/_ready") && statusCode == 200)
            return LogEventLevel.Debug;

        if (ex is not null || statusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        return statusCode >= 400
            ? LogEventLevel.Warning
            : LogEventLevel.Debug;
    };

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
    };
});

// Documentación de la API: documento OpenAPI nativo (/openapi/v1.json) y la UI
// interactiva de Scalar (/scalar). Habilitada por defecto en entornos no productivos
// (Development, Staging). En Production se puede habilitar temporalmente estableciendo
// ApiDocs__Enabled=true.
var apiDocsEnabled = !app.Environment.IsProduction()
    || app.Configuration.GetValue<bool>("ApiDocs:Enabled");

// Las claves de los dos ejes del portal —el token y la dirección— salen del cuerpo, y el
// particionador del limitador es síncrono, así que las deja alguien por delante de él.
app.UsePublicRateLimitPartitions();

// El limitador va antes de la autenticación: una inundación contra el portal se corta
// sin gastar una validación de token por petición.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (apiDocsEnabled)
{
    // La documentación es navegable sin login (la fallback policy la bloquearía), igual que
    // en los servicios hermanos. Lo que la mantiene fuera de internet no es un token sino la
    // red: el nginx del frontend reenvía los dos prefijos de la API y ninguna otra ruta, y
    // este servicio no tiene hostname propio.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sorteo360 — Convocatorias API");
    }).AllowAnonymous();
}

// Auto-discover and map feature endpoints.
app.MapEndpoints(Assembly.GetExecutingAssembly());

app.Run();

// Exposed so the integration test WebApplicationFactory can reference the host.
public partial class Program;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Sorteo360.Notifications.Infrastructure.Database;
using Sorteo360.Notifications.Web.Auth;
using Sorteo360.Notifications.Web.Configuration;
using Sorteo360.Notifications.Web.OpenApi;
using Sorteo360.Platform.Auth;
using Sorteo360.Platform.Configuration;
using Sorteo360.Platform.GlobalEndpoints;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Remove the Server response header.
builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

// Register web services: API versioning, documentation and logging (Serilog).
// El transformer del documento OpenAPI es el de este servicio: fija el título y la
// descripción que muestra Scalar.
builder.Services.AddWebServices(builder.Configuration, builder.Host, options =>
    options.AddDocumentTransformer<NotificationsDocumentTransformer>());

// Autenticación (JWT de Keycloak) y las políticas por rol funcional de este servicio,
// con la fallback policy que exige usuario autenticado en todo endpoint que no diga
// lo contrario.
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationPolicies();

// Register data services: PostgreSQL, in-memory cache and health checks.
builder.Services.AddDataServices<NotificationsDbContext>(builder.Configuration);

// Register domain services: repositories and application services.
builder.Services.AddDomainServices(builder.Configuration, builder.Environment);

// Auto-discover and register feature endpoints. El chasis se recorre solo, así que
// los endpoints de salud entran sin nombrarlos aquí.
builder.Services.AddEndpoints(builder.Configuration, Assembly.GetExecutingAssembly());

var app = builder.Build();

// Las migraciones de EF Core son la única autoridad del esquema: se aplican las
// pendientes en cada arranque, antes de aceptar tráfico. Si la base no está
// disponible el servicio falla rápido (Compose lo reintenta vía depends_on/healthcheck).
// EF serializa migraciones concurrentes con un lock a nivel de base de datos.
// En "IntegrationTesting" se omite: la factory con Testcontainers ejecuta las mismas
// migraciones contra su contenedor, y el smoke test corre sin base de datos real.
if (!app.Environment.IsEnvironment("IntegrationTesting"))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
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

        if (path is not null && path.StartsWith("/_health") && statusCode == 200)
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

app.UseAuthentication();
app.UseAuthorization();

if (apiDocsEnabled)
{
    // La documentación es navegable sin login (la fallback policy la bloquearía), igual que
    // en los servicios hermanos. Lo que la mantiene fuera de internet es la red y no un
    // token: este servicio no tiene hostname propio salvo el del webhook, que enruta una
    // sola ruta.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sorteo360 — Notifications API");
    }).AllowAnonymous();
}

// Auto-discover and map feature endpoints.
app.MapEndpoints(Assembly.GetExecutingAssembly());

app.Run();

// Exposed so the integration test WebApplicationFactory can reference the host.
public partial class Program;

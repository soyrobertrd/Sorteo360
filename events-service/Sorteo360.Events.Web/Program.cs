using System.IO.Compression;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.Configuration;
using Sorteo360.Events.Web.OpenApi;
using Sorteo360.Platform.Auth;
using Sorteo360.Platform.Configuration;
using Sorteo360.Platform.GlobalEndpoints;
using Sorteo360.Platform.Http;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Remove the Server response header.
builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

// Register web services: API versioning, documentation and logging (Serilog).
// Los transformers del documento OpenAPI son los de este servicio: el título y la
// descripción que muestra Scalar, y el rol requerido al pie de cada operación.
builder.Services.AddWebServices(builder.Configuration, builder.Host, options =>
{
    options.AddDocumentTransformer<EventsDocumentTransformer>();
    options.AddOperationTransformer<RequiredRoleOperationTransformer>();
});

// Autenticación (JWT de Keycloak) y las políticas por rol funcional de este servicio.
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationPolicies();

// La IP real de quien llama: en prod hay dos proxies delante (Caddy y el nginx del frontend,
// que enruta /v1/* a este servicio). Con uno solo, el límite de alta de cuentas sería global.
builder.Services.AddProxyHeaders(proxyHopCount: 2);

// Usuario actual (claim sub) para estampar los ids de auditoría.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddScoped<IAccountAccessChecker, DatabaseAccountAccessChecker>();

// Register data services: PostgreSQL, in-memory cache and health checks.
builder.Services.AddDataServices<EventsDbContext>(builder.Configuration);

// Register domain services: repositories and application services.
builder.Services.AddDomainServices(builder.Configuration);

// S1: límite de frecuencia por IP en rutas públicas de alta/activación (/v1/accounts/*).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("accounts-public", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

// A31 / R15: compresión Brotli/Gzip para respuestas JSON grandes (sorteo con 10.000 premios / padrón de 25.000).
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json", "application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

// Auto-discover and register feature endpoints. El chasis se recorre solo, así que
// los endpoints de salud entran sin nombrarlos aquí.
builder.Services.AddEndpoints(builder.Configuration, Assembly.GetExecutingAssembly());

var app = builder.Build();

// Este paso va primero en la tubería: reescribe la IP y el esquema con lo que dice
// Traefik, para que todo lo que venga después, como un límite por IP, vea al cliente y no
// al proxy.
app.UseForwardedHeaders();
app.UseResponseCompression();

// Las migraciones de EF Core son la única autoridad del esquema: se aplican las
// pendientes en cada arranque, antes de aceptar tráfico. Si la base no está
// disponible el servicio falla rápido (Compose lo reintenta vía depends_on/healthcheck).
// EF serializa migraciones concurrentes con un lock a nivel de base de datos.
// En "IntegrationTesting" se omite: la factory con Testcontainers ejecuta las mismas
// migraciones contra su contenedor, y el smoke test corre sin base de datos real.
if (!app.Environment.IsEnvironment("IntegrationTesting"))
{
    try
    {
        await using var migrationScope = app.Services.CreateAsyncScope();
        var dbContext = migrationScope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex) when (app.Environment.IsDevelopment())
    {
        Log.Warning(ex, "PostgreSQL no está disponible en desarrollo local; se omiten migraciones automáticas al arrancar.");
    }
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
// apidocs__enabled=true (idealmente protegida tras autenticación cuando exista).
var apiDocsEnabled = !app.Environment.IsProduction()
    || app.Configuration.GetValue<bool>("ApiDocs:Enabled");

// Autenticación (JWT de Keycloak) y autorización (políticas por rol + fallback
// que exige usuario autenticado en todo endpoint sin AllowAnonymous).
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<RequireAccountClaimMiddleware>();

if (apiDocsEnabled)
{
    // La documentación es navegable sin login (la fallback policy la bloquearía).
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sorteo360 — Events & Raffles API");
    }).AllowAnonymous();
}

// Auto-discover and map feature endpoints.
app.MapEndpoints(Assembly.GetExecutingAssembly());

app.Run();

// Exposed so the integration test WebApplicationFactory can reference the host.
public partial class Program;

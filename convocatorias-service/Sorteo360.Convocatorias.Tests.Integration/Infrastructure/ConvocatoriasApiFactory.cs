using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sorteo360.Convocatorias.Web;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// <c>WebApplicationFactory</c> para tests de integración. Arranca la API real con
/// el entorno "IntegrationTesting" y sin dependencias externas reales: usa caché en
/// memoria y reemplaza los health checks por uno saludable, de modo que los tests que no
/// tocan la base de datos no requieran un PostgreSQL levantado.
/// </summary>
public sealed class ConvocatoriasApiFactory : WebApplicationFactory<IApiMarker>
{
    /// <summary>Una cadena ficticia: estos tests no se conectan a ninguna base.</summary>
    private const string UnusedConnectionString = "Host=localhost;Database=test;Username=test;Password=test";

    /// <summary>El CRM simulado.</summary>
    public CrmStub Crm { get; } = new();

    /// <summary>events-service simulado.</summary>
    public EventsStub Events { get; } = new();

    /// <summary>notification-service simulado.</summary>
    public NotificationStub Notifications { get; } = new();

    /// <summary>
    /// IP desde la que el host ve llegar la conexión. Es la del nginx del frontend: el
    /// último salto antes del servicio, y el que tiene que estar en una red de confianza
    /// para que <c>UseForwardedHeaders</c> mire la cabecera.
    /// </summary>
    private const string PeerIpAddress = "10.1.2.3";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        // Va con UseSetting, como en ConvocatoriasApiDbFactory, y no con una variable de entorno.
        // La variable es de todo el proceso: el Dispose de una fábrica la borraba mientras otra
        // clase de test, que xUnit corre en paralelo, todavía estaba arrancando su host.
        builder.UseSetting("ConnectionStrings:Postgres", UnusedConnectionString);
        builder.UseStubbedServiceIdentity(Crm.Url, Events.Url, Notifications.Url);

        builder.ConfigureTestServices(services =>
        {
            // Esquema de autenticación de prueba en lugar del JWT de Keycloak.
            services.AddTestAuthentication();
            services.AddStubbedServiceToken();

            // El TestServer no trae IP de origen, y sin ella UseForwardedHeaders no mira
            // X-Forwarded-For: el primer salto que recorre tiene que venir de una red de
            // confianza. Se pone la del proxy vecino antes de todo lo demás.
            services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(PeerIpAddress));

            // Reemplazar los health checks reales por uno saludable etiquetado "ready",
            // para que /_ready no dependa de un PostgreSQL real.
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new HealthCheckRegistration(
                    "ready-test",
                    new HealthyCheck(),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["ready"]));
            });
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Crm.Dispose();
            Events.Dispose();
            Notifications.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Health check trivial que siempre reporta saludable.</summary>
    private sealed class HealthyCheck : IHealthCheck
    {
        /// <inheritdoc />
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}

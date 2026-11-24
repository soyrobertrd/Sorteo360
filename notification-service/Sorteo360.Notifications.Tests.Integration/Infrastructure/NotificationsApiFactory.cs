using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sorteo360.Notifications.Web;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// <c>WebApplicationFactory</c> para tests de integración. Arranca la API real con
/// el entorno "IntegrationTesting" y sin dependencias externas reales: usa caché en
/// memoria y reemplaza los health checks por uno saludable, de modo que los tests que no
/// tocan la base de datos no requieran un PostgreSQL levantado.
/// </summary>
public sealed class NotificationsApiFactory : WebApplicationFactory<IApiMarker>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        // La cadena entra por UseSetting y NO por una variable de entorno del proceso.
        // UseSetting llega a IConfiguration antes de que se construya el contenedor DI, que es
        // lo que AddDataServices necesita; una variable de entorno además es estado global, y
        // dos clases de test con su propia factory se la pisaban entre ellas — la que terminara
        // primero la borraba y a la otra le fallaba el arranque del host.
        // Cadena ficticia: estos tests no se conectan a una base real.
        builder.UseSetting(
            "ConnectionStrings:Postgres", "Host=localhost;Database=test;Username=test;Password=test");

        builder.ConfigureTestServices(services =>
        {
            // Esquema de autenticación de prueba en lugar del JWT de Keycloak.
            services.AddTestAuthentication();

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

    /// <summary>Health check trivial que siempre reporta saludable.</summary>
    private sealed class HealthyCheck : IHealthCheck
    {
        /// <inheritdoc />
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}

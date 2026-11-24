using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Web;
using Sorteo360.Events.Web.Auth;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// <c>WebApplicationFactory</c> para tests de integración. Arranca la API real con
/// el entorno "IntegrationTesting" y sin dependencias externas reales: usa caché en
/// memoria y reemplaza los health checks por uno saludable, de modo que el smoke test
/// no requiera un PostgreSQL levantado.
/// </summary>
public sealed class EventsApiFactory : WebApplicationFactory<IApiMarker>
{
    private const string PostgresEnvVar = "connectionstrings__postgres";

    public EventsApiFactory()
    {
        // La configuración se inyecta por variables de entorno (no por ConfigureApp-
        // Configuration) porque Program lee builder.Configuration de forma temprana,
        // antes de que se apliquen los callbacks de la WebApplicationFactory.
        // Cadena ficticia: el smoke test no se conecta a una DB real.
        Environment.SetEnvironmentVariable(PostgresEnvVar, "Host=localhost;Database=test;Username=test;Password=test");
    }

    /// <summary>El CRM simulado: acá solo hace falta para que la aplicación arranque.</summary>
    public CrmDirectoryStub Crm { get; } = new();

    /// <summary>notification-service simulado, por lo mismo: nada de acá manda un aviso.</summary>
    public NotificationStub Notifications { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");
        builder.UseStubbedServiceIdentity(Crm.Url, Notifications.Url);
        builder.UseFilesystemFileStore(Path.Combine(Path.GetTempPath(), $"pgti360-events-files-{Guid.NewGuid():N}"));

        builder.ConfigureTestServices(services =>
        {
            // Esquema de autenticación de prueba en lugar del JWT de Keycloak.
            services.AddTestAuthentication();
            services.AddStubbedServiceToken();
            services.RemoveAll<IAccountAccessChecker>();
            services.AddScoped<IAccountAccessChecker, DefaultAccountOnlyAccessChecker>();

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
            Environment.SetEnvironmentVariable(PostgresEnvVar, null);
            Crm.Dispose();
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

    private sealed class DefaultAccountOnlyAccessChecker : IAccountAccessChecker
    {
        public Task<bool> IsActiveAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(accountId == Account.DefaultBanreservasAccountId);
    }
}

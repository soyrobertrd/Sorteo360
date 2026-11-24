using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sorteo360.Notifications.Infrastructure.Database;
using Sorteo360.Notifications.Web;
using Testcontainers.PostgreSql;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// <c>WebApplicationFactory</c> respaldada por un contenedor PostgreSQL real.
/// Arranca el contenedor, aplica el esquema a través de EF Core y configura
/// la aplicación para apuntar al contenedor en lugar de la base de datos real.
/// </summary>
public class NotificationsApiDbFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("notifications")
        .Build();

    /// <summary>
    /// Cadena de conexión del contenedor, para que un test pueda comprobar contra la base lo que la
    /// API dice haber escrito. Leer por SQL y no por el <c>DbContext</c> es a propósito: las clases
    /// de persistencia son internas de Infrastructure, y un test que las usara comprobaría el modelo
    /// en vez de las columnas.
    /// </summary>
    public string ConnectionString => _postgres.GetConnectionString();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplySchemaAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        // UseSetting inyecta el valor en IConfiguration antes de que se construya
        // el contenedor DI, por lo que AddDbContext en DataServicesConfiguration
        // leerá la cadena de conexión del contenedor automáticamente.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            // Esquema de autenticación de prueba en lugar del JWT de Keycloak.
            services.AddTestAuthentication();

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

    /// <summary>
    /// Aplica el esquema ejecutando las migraciones de EF Core, el mismo DDL que
    /// corre producción al arrancar. Así los tests validan las migraciones reales
    /// en lugar de un esquema sintetizado desde el modelo (EnsureCreated).
    /// </summary>
    private async Task ApplySchemaAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await context.Database.MigrateAsync();
    }

    private sealed class HealthyCheck : IHealthCheck
    {
        /// <inheritdoc />
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Web;
using Testcontainers.PostgreSql;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// <c>WebApplicationFactory</c> respaldada por un contenedor PostgreSQL real.
/// Arranca el contenedor, aplica el esquema a través de EF Core y configura
/// la aplicación para apuntar al contenedor en lugar de la base de datos real.
/// </summary>
public class ConvocatoriasApiDbFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("convocatorias")
        .Build();

    /// <summary>
    /// Los tres vecinos simulados. Cada clase de test tiene los suyos, así que puede
    /// reprogramarlos sin afectar a las demás.
    /// </summary>
    public CrmStub Crm { get; } = new();

    public EventsStub Events { get; } = new();

    public NotificationStub Notifications { get; } = new();

    /// <summary>La IP del último proxy, igual que en <see cref="ConvocatoriasApiFactory"/>.</summary>
    private const string PeerIpAddress = "10.1.2.3";

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplySchemaAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Crm.Dispose();
        Events.Dispose();
        Notifications.Dispose();
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
        builder.UseStubbedServiceIdentity(Crm.Url, Events.Url, Notifications.Url);

        builder.ConfigureTestServices(services =>
        {
            // Esquema de autenticación de prueba en lugar del JWT de Keycloak.
            services.AddTestAuthentication();
            services.AddStubbedServiceToken();
            services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(PeerIpAddress));

            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new HealthCheckRegistration(
                    "ready-test",
                    new HealthyCheck(),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["ready"]));
            });

            ConfigureSendTransport(services);
        });
    }

    /// <summary>
    /// Qué hay detrás de la costura de envío. Por omisión, un transporte que <b>nunca contesta</b>.
    /// <para>
    /// Es lo que vuelve determinista a toda clase que afirma sobre el estado justo después del
    /// <c>202</c>. La entrega corre en un worker, así que con un transporte que sí contesta esas
    /// afirmaciones serían una carrera contra él: "las filas siguen <c>pending</c>" pasaría o
    /// fallaría según quién llegara primero. Colgarlo modela además algo que pasa de verdad —
    /// notification-service tardando— y no algo inventado para el test.
    /// </para>
    /// <para>
    /// Quien quiere entrega la pide: <c>CampaignHandoffFactory</c> pone su doble y
    /// <c>StubNotificationFactory</c> deja el <c>StubCampaignSendGateway</c> de producción.
    /// </para>
    /// </summary>
    protected virtual void ConfigureSendTransport(IServiceCollection services) =>
        services.AddSingleton<ICampaignSendGateway>(new StalledSendGateway());

    /// <summary>
    /// Aplica el esquema ejecutando las migraciones de EF Core, el mismo DDL que
    /// corre producción al arrancar. Así los tests validan las migraciones reales
    /// en lugar de un esquema sintetizado desde el modelo (EnsureCreated).
    /// </summary>
    private async Task ApplySchemaAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.MigrateAsync();
    }

    private sealed class HealthyCheck : IHealthCheck
    {
        /// <inheritdoc />
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}

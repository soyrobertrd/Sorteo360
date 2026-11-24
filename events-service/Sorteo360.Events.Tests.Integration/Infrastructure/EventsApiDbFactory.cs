using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Web;
using Testcontainers.PostgreSql;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// <c>WebApplicationFactory</c> respaldada por un contenedor PostgreSQL real.
/// Arranca el contenedor, aplica el esquema a través de EF Core y configura
/// la aplicación para apuntar al contenedor en lugar de la base de datos real.
/// </summary>
public class EventsApiDbFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("events")
        .Build();

    /// <summary>
    /// El CRM simulado del que sale el directorio de OPT. Cada clase de test tiene el suyo, así que
    /// puede reprogramarlo sin afectar a las demás.
    /// </summary>
    public CrmDirectoryStub Crm { get; } = new();

    /// <summary>
    /// notification-service simulado, por donde salen los avisos de una cotización. Uno por clase,
    /// igual que el CRM, para que un test que lo tumba no arrastre a los demás.
    /// </summary>
    public NotificationStub Notifications { get; } = new();

    /// <summary>
    /// El reloj de la aplicación. Corre con el del sistema; un test que depende del día de hoy lo
    /// fija con <see cref="TestClock.Pin"/>.
    /// </summary>
    public TestClock Clock { get; } = new();

    /// <summary>
    /// Carpeta donde el proveedor de sistema de archivos deja los bytes. Es propia de cada
    /// factory, así que una clase de test puede borrar un archivo del disco sin afectar a otra.
    /// </summary>
    public string FileStoreRootPath { get; } =
        Path.Combine(Path.GetTempPath(), $"pgti360-events-files-{Guid.NewGuid():N}");

    /// <summary>
    /// Vigencia de la copia del directorio. Por omisión la de producción: los tests que no van
    /// sobre el directorio hacen una sola consulta al CRM y sirven de la caché el resto.
    /// </summary>
    protected virtual int OptDirectoryTtlMinutes => 15;

    /// <summary>
    /// Vigencia de la copia del directorio de empresas. Es un ajuste aparte del de OPT, igual que en
    /// producción, para que una clase que va sobre una copia pueda vencerla sin vencer la otra.
    /// </summary>
    protected virtual int CompanyDirectoryTtlMinutes => 15;

    /// <summary>
    /// Tamaño máximo de un archivo. Por omisión el acordado; una clase que va sobre el límite lo
    /// baja para no tener que mandar 25 MB por la tubería de prueba.
    /// </summary>
    protected virtual long MaxUploadSizeBytes => 25L * 1024 * 1024;

    /// <summary>
    /// Fotos que admite una ficha. Por omisión el acordado; una clase que va sobre el cupo lo
    /// baja para no tener que subir veinte.
    /// </summary>
    protected virtual int MaxPhotosPerServiceSupplier => 20;

    /// <summary>
    /// Adjuntos que admite una propuesta. Por omisión el acordado; una clase que va sobre el cupo
    /// lo baja para no tener que subir diez.
    /// </summary>
    protected virtual int MaxAttachmentsPerProposal => 10;

    /// <summary>
    /// Fotos de galería que admite un evento. Por omisión el acordado; una clase que va sobre el
    /// cupo lo baja para no tener que subir veinte.
    /// </summary>
    protected virtual int MaxGalleryImagesPerEvent => 20;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplySchemaAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Crm.Dispose();
        Notifications.Dispose();
        await _postgres.DisposeAsync();

        if (Directory.Exists(FileStoreRootPath))
            Directory.Delete(FileStoreRootPath, recursive: true);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        // UseSetting inyecta el valor en IConfiguration antes de que se construya
        // el contenedor DI, por lo que AddDbContext en DataServicesConfiguration
        // leerá la cadena de conexión del contenedor automáticamente.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("OptDirectory:SnapshotTtlMinutes", OptDirectoryTtlMinutes.ToString());
        builder.UseSetting("CompanyDirectory:SnapshotTtlMinutes", CompanyDirectoryTtlMinutes.ToString());
        builder.UseSetting("FileStore:MaxSizeBytes", MaxUploadSizeBytes.ToString());
        builder.UseSetting("FileStore:MaxPhotosPerServiceSupplier", MaxPhotosPerServiceSupplier.ToString());
        builder.UseSetting("FileStore:MaxAttachmentsPerProposal", MaxAttachmentsPerProposal.ToString());
        builder.UseSetting("FileStore:MaxGalleryImagesPerEvent", MaxGalleryImagesPerEvent.ToString());
        builder.UseFilesystemFileStore(FileStoreRootPath);
        builder.UseStubbedServiceIdentity(Crm.Url, Notifications.Url);

        builder.ConfigureTestServices(services =>
        {
            // Esquema de autenticación de prueba en lugar del JWT de Keycloak.
            services.AddTestAuthentication();
            services.AddStubbedServiceToken();
            services.AddSingleton<TimeProvider>(Clock);

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
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await context.Database.MigrateAsync();
    }

    private sealed class HealthyCheck : IHealthCheck
    {
        /// <inheritdoc />
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}

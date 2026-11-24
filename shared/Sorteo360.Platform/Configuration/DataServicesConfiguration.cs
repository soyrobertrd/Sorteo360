using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sorteo360.Platform.Configuration;

/// <summary>
/// Registra los servicios de datos: PostgreSQL (EF Core), la caché distribuida
/// en memoria y los health checks asociados. El <c>DbContext</c> lo aporta el
/// servicio como parámetro de tipo, así que el chasis no conoce ninguno.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DataServicesConfiguration
{
    /// <summary>Registra el acceso a datos, la caché y los health checks de la API.</summary>
    /// <typeparam name="TContext">DbContext del servicio que llama.</typeparam>
    public static IServiceCollection AddDataServices<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Postgres'.");

        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(postgresConnectionString));

        // Caché en memoria: no hay consumidor de IDistributedCache hoy; se mantiene
        // la interfaz para que un futuro backend distribuido sea config-only.
        services.AddDistributedMemoryCache();
        AddDataHealthChecks(services, postgresConnectionString);

        return services;
    }

    /// <summary>Registra los health checks de base de datos.</summary>
    private static void AddDataHealthChecks(IServiceCollection services, string postgresConnectionString)
    {
        var healthChecks = services.AddHealthChecks();

        // PostgreSQL es crítico para la readiness del servicio.
        healthChecks.AddNpgSql(
            postgresConnectionString,
            name: "postgres",
            tags: ["ready", "database", "critical"]);
    }
}

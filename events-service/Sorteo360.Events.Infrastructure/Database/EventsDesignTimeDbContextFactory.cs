using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Factory de diseño para herramientas de EF Core en tiempo de compilación.
/// Permite ejecutar 'dotnet ef migrations add' desde el proyecto Infrastructure
/// sin necesidad de levantar el proyecto Web completo.
/// Solo se usa por el CLI de EF Core, no se registra en el contenedor de DI.
/// </summary>
internal sealed class EventsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<EventsDbContext>
{
    /// <summary>
    /// Crea una instancia de <see cref="EventsDbContext"/> para herramientas de diseño.
    /// Lee la cadena de conexión desde la variable de entorno <c>connectionstrings__postgres</c>;
    /// si no está definida, usa una cadena de desarrollo local.
    /// </summary>
    public EventsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("connectionstrings__postgres")
            ?? "Host=localhost;Port=5432;Database=events;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<EventsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new EventsDbContext(optionsBuilder.Options);
    }
}

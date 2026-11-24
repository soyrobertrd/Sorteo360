using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Factory de diseño para herramientas de EF Core en tiempo de compilación.
/// Permite ejecutar 'dotnet ef migrations add' desde el proyecto Infrastructure
/// sin necesidad de levantar el proyecto Web completo.
/// Solo se usa por el CLI de EF Core, no se registra en el contenedor de DI.
/// </summary>
internal sealed class ConvocatoriasDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConvocatoriasDbContext>
{
    /// <summary>
    /// Crea una instancia de <see cref="ConvocatoriasDbContext"/> para herramientas de diseño.
    /// Lee la cadena de conexión desde la variable de entorno <c>ConnectionStrings__Postgres</c>;
    /// si no está definida, usa una cadena de desarrollo local.
    /// </summary>
    public ConvocatoriasDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=convocatorias;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ConvocatoriasDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ConvocatoriasDbContext(optionsBuilder.Options);
    }
}

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sorteo360.Platform.GlobalEndpoints;

/// <summary>
/// Descubre las implementaciones de <see cref="IEndpoint"/> de un conjunto de ensamblados y
/// ejecuta sus registros de servicios y sus definiciones de rutas. El ensamblado del chasis
/// forma parte del conjunto siempre, así que un servicio que solo pasa el suyo conserva igual
/// los endpoints de salud, de los que dependen el <c>HEALTHCHECK</c> de su imagen y el
/// <c>depends_on</c> de Compose.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Invoca <see cref="IEndpoint.AddServices"/> en cada endpoint descubierto, tanto en el
    /// ensamblado del chasis como en los que pase quien llama.
    /// </summary>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        foreach (var type in GetEndpointTypes(assemblies))
        {
            type.GetMethod(nameof(IEndpoint.AddServices))!
                .Invoke(null, [services, configuration]);
        }

        return services;
    }

    /// <summary>
    /// Invoca <see cref="IEndpoint.DefineEndpoints"/> en cada endpoint descubierto, tanto en el
    /// ensamblado del chasis como en los que pase quien llama.
    /// </summary>
    public static IApplicationBuilder MapEndpoints(this WebApplication app, params Assembly[] assemblies)
    {
        foreach (var type in GetEndpointTypes(assemblies))
        {
            type.GetMethod(nameof(IEndpoint.DefineEndpoints))!
                .Invoke(null, [app]);
        }

        return app;
    }

    /// <summary>
    /// Recorre el ensamblado del chasis más los que pase quien llama, sin repetir ninguno:
    /// el chasis aporta los endpoints de salud aunque nadie los pida, y una segunda pasada
    /// sobre el mismo ensamblado duplicaría lo que registra <see cref="IEndpoint.AddServices"/>.
    /// </summary>
    private static IEnumerable<TypeInfo> GetEndpointTypes(Assembly[] assemblies) =>
        assemblies
            .Prepend(typeof(EndpointExtensions).Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(t =>
                t is { IsAbstract: false, IsInterface: false } &&
                t.IsAssignableTo(typeof(IEndpoint)));
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sorteo360.Platform.Files;

/// <summary>
/// Registro del almacenamiento de archivos: elige el proveedor por configuración y lo publica
/// como <see cref="IFileStore"/>, que es lo único que ve el resto de la aplicación.
/// <para>
/// Falla al arrancar si la configuración no alcanza. Un servicio que guarda archivos donde no
/// debía es peor que uno que no arranca, y el primer usuario que sube algo no es el momento de
/// enterarse.
/// </para>
/// </summary>
public static class FileStoreExtensions
{
    /// <summary>
    /// Registra el proveedor indicado por <c>FileStore:Provider</c>.
    /// </summary>
    /// <param name="configuration">Configuración del host, de donde se lee la sección <c>FileStore</c>.</param>
    public static IServiceCollection AddFileStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration.GetSection(FileStoreSettings.SectionName).Get<FileStoreSettings>()
            ?? new FileStoreSettings();

        var provider = settings.Provider?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(provider))
        {
            throw new InvalidOperationException(
                "Falta la configuración del almacenamiento de archivos: se requiere 'FileStore:Provider'.");
        }

        if (provider != FilesystemFileStore.ProviderName)
        {
            throw new InvalidOperationException(
                $"Proveedor de almacenamiento de archivos desconocido: '{settings.Provider}'. " +
                $"Valores admitidos: '{FilesystemFileStore.ProviderName}'.");
        }

        var rootPath = settings.Filesystem.RootPath;

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException(
                $"Falta la configuración del proveedor '{FilesystemFileStore.ProviderName}': " +
                "se requiere 'FileStore:Filesystem:RootPath'.");
        }

        services.AddSingleton<IFileStore>(_ => new FilesystemFileStore(rootPath));

        return services;
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La misma aplicación de siempre, con el almacenamiento envuelto en
/// <see cref="FaultyFileStore"/> para poder tirarlo abajo a mitad de un test.
/// </summary>
public sealed class FileStoreOutageApiDbFactory : EventsApiDbFactory
{
    private FaultyFileStore? _store;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Se registra después de AddFileStore, así que este es el IFileStore que resuelve la
            // aplicación; el proveedor de sistema de ficheros queda dentro, haciendo el trabajo
            // de verdad mientras no haya avería armada.
            services.AddSingleton<IFileStore>(_ => Wrapped);
        });
    }

    /// <summary>Envoltorio único: el mismo objeto que el test manipula y que resuelve la app.</summary>
    private FaultyFileStore Wrapped =>
        _store ??= new FaultyFileStore(new FilesystemFileStore(FileStoreRootPath));

    /// <summary>Arma una avería y la desarma al salir del bloque <c>using</c>.</summary>
    public IDisposable Failing(FileStoreFault fault) => Wrapped.Arm(fault);
}

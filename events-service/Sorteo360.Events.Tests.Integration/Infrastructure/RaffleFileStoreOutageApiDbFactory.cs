using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La aplicación del sorteo, con la puerta en manos del test, y además el almacenamiento envuelto en
/// <see cref="FaultyFileStore"/>: una entrega necesita un sorteo arrancado y una firma que guardar.
/// </summary>
public class RaffleFileStoreOutageApiDbFactory : RaffleAttendanceApiDbFactory
{
    private FaultyFileStore? _store;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IFileStore>(_ => Wrapped));
    }

    /// <summary>Envoltorio único: el mismo objeto que el test manipula y que resuelve la app.</summary>
    private FaultyFileStore Wrapped =>
        _store ??= new FaultyFileStore(new FilesystemFileStore(FileStoreRootPath));

    /// <summary>Arma una avería y la desarma al salir del bloque <c>using</c>.</summary>
    public IDisposable Failing(FileStoreFault fault) => Wrapped.Arm(fault);
}

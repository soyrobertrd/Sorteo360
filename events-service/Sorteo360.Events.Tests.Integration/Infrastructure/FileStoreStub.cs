using Microsoft.AspNetCore.Hosting;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// El almacenamiento de archivos de los tests: el proveedor de sistema de ficheros sobre una
/// carpeta temporal.
/// <para>
/// No se sustituye <c>IFileStore</c> por un doble a propósito. El proveedor real es el que
/// escribe bytes, calcula el resumen y falla cuando el archivo no está, y nada de eso se
/// ejercitaría contra un objeto en memoria.
/// </para>
/// </summary>
public static class FileStoreStub
{
    /// <summary>Elige el proveedor de sistema de ficheros y le fija su carpeta raíz.</summary>
    public static void UseFilesystemFileStore(this IWebHostBuilder builder, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("FileStore:Provider", "filesystem");
        builder.UseSetting("FileStore:Filesystem:RootPath", rootPath);
    }
}

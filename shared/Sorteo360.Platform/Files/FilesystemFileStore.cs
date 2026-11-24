using System.Security.Cryptography;

namespace Sorteo360.Platform.Files;

/// <summary>
/// Almacenamiento sobre una carpeta local. Es el proveedor del desarrollo local y de los tests,
/// nunca el de producción.
/// <para>
/// No es un doble del almacenamiento real: es una segunda implementación de verdad, y
/// justamente por lo poco que se parece a uno en red —sin HTTP, sin identificadores remotos, sin
/// comparticiones— es lo que demuestra que el seam es abstracto y no la forma de un proveedor
/// concreto.
/// </para>
/// </summary>
public sealed class FilesystemFileStore : IFileStore
{
    /// <summary>Nombre con el que este proveedor queda registrado en cada archivo que guarda.</summary>
    public const string ProviderName = "filesystem";

    /// <summary>Tamaño del bloque de copia; el mismo que usa <c>Stream.CopyTo</c> por omisión.</summary>
    private const int CopyBufferSize = 81920;

    /// <summary>Extensión que se conserva en la clave: solo letras y dígitos, y corta.</summary>
    private const int MaxExtensionLength = 10;

    private readonly string _rootPath;

    public FilesystemFileStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    }

    /// <inheritdoc />
    public async Task<StoredFileLocation> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);

        var storageKey = BuildStorageKey(fileName);
        var fullPath = ResolvePath(storageKey);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            long sizeBytes;
            byte[] checksum;

            // Se escribe y se resume en la misma pasada: el contenido puede ser grande y no
            // hay motivo para tenerlo entero en memoria ni para releerlo desde el disco.
            await using (var target = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
            {
                (sizeBytes, checksum) = await CopyAndHashAsync(content, target, cancellationToken);
            }

            return new StoredFileLocation(
                ProviderName,
                storageKey,
                sizeBytes,
                Convert.ToHexStringLower(checksum));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Un archivo a medio escribir es peor que ninguno: nadie lo referencia todavía,
            // así que se limpia antes de que la clave se pierda con la excepción.
            TryDeleteFile(fullPath);

            throw new FileStoreUnavailableException(
                "No se pudo escribir en el almacenamiento de archivos.", exception);
        }
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolvePath(storageKey);

        try
        {
            Stream stream = new FileStream(
                fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);

            return Task.FromResult(stream);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileNotFoundInStoreException(storageKey, exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileStoreUnavailableException(
                "No se pudo leer del almacenamiento de archivos.", exception);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolvePath(storageKey);

        try
        {
            // File.Delete no se queja si el archivo no está, que es justo la idempotencia
            // que promete el seam.
            File.Delete(fullPath);
            return Task.CompletedTask;
        }
        catch (DirectoryNotFoundException)
        {
            return Task.CompletedTask;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileStoreUnavailableException(
                "No se pudo borrar del almacenamiento de archivos.", exception);
        }
    }

    /// <summary>
    /// Copia el contenido calculando su tamaño y su SHA-256 en la misma pasada.
    /// </summary>
    private static async Task<(long SizeBytes, byte[] Checksum)> CopyAndHashAsync(
        Stream source,
        Stream target,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[CopyBufferSize];
        long sizeBytes = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sizeBytes += read;
        }

        return (sizeBytes, hash.GetHashAndReset());
    }

    /// <summary>
    /// Arma la clave de un archivo nuevo. Va repartida por año y mes para que ninguna carpeta
    /// crezca sin límite, y el nombre es un guid: el nombre original lo guarda el dominio, y
    /// dejarlo entrar acá haría colisionar dos "cotizacion.pdf".
    /// </summary>
    private static string BuildStorageKey(string fileName)
    {
        var now = DateTimeOffset.UtcNow;
        return $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{SafeExtension(fileName)}";
    }

    /// <summary>
    /// Extensión del nombre original si es inofensiva, cadena vacía si no. Se conserva solo
    /// para que la carpeta sea legible por una persona; nada la interpreta.
    /// </summary>
    private static string SafeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (extension.Length < 2 || extension.Length > MaxExtensionLength + 1)
            return string.Empty;

        return extension.Skip(1).All(char.IsAsciiLetterOrDigit)
            ? extension.ToLowerInvariant()
            : string.Empty;
    }

    /// <summary>
    /// Traduce una clave a una ruta absoluta dentro de la raíz. Una clave que se escapa de ella
    /// es una fila corrupta, no un archivo ausente: se distingue a propósito, porque tratarla
    /// como ausente escondería el problema en vez de mostrarlo.
    /// </summary>
    private string ResolvePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));

        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new FileStoreException(
                $"La clave de almacenamiento '{storageKey}' apunta fuera de la carpeta raíz.");
        }

        return fullPath;
    }

    /// <summary>Borra un archivo a medio escribir sin dejar que su fallo tape el original.</summary>
    private static void TryDeleteFile(string fullPath)
    {
        try
        {
            File.Delete(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // No hay nada mejor que hacer: lo que importa es la excepción que va a salir.
        }
    }
}

namespace Sorteo360.Events.Core.Files;

/// <summary>
/// Un archivo guardado, visto por lo único que el almacenamiento sabe de él: dónde está, de qué
/// tamaño, con qué resumen, y quién lo subió y cuándo.
/// <para>
/// Para qué sirve el archivo no vive acá: eso es una fila de enlace aparte (la foto de un
/// proveedor, el adjunto de una propuesta). Son cosas que cambian por motivos distintos, y
/// separarlas es lo que permite cambiar de almacenamiento sin tocar una regla de negocio.
/// </para>
/// <para>
/// La fila se escribe una vez y no se altera: reemplazar un archivo es subir otro y reapuntar el
/// enlace. La única excepción es <see cref="MissingSince"/>, que registra el día que el
/// almacenamiento dejó de tener los bytes.
/// </para>
/// </summary>
public sealed record StoredFile
{
    public Guid Id { get; init; }

    /// <summary>Proveedor que guardó los bytes; queda registrado porque puede cambiar con el tiempo.</summary>
    public string Provider { get; init; }

    /// <summary>Clave opaca con la que el proveedor devuelve el contenido. El dominio nunca la interpreta.</summary>
    public string StorageKey { get; init; }

    /// <summary>Nombre original, el que le sirve a una persona para reconocer el archivo.</summary>
    public string FileName { get; init; }

    public string ContentType { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>SHA-256 del contenido en hexadecimal minúsculo.</summary>
    public string Checksum { get; init; }

    /// <summary>
    /// Desde cuándo el almacenamiento dice que el archivo ya no está. <c>null</c> mientras esté.
    /// Un almacenamiento caído nunca sella esta fecha: eso destruiría el registro de qué se
    /// perdió justo cuando hace falta.
    /// </summary>
    public DateTimeOffset? MissingSince { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedById { get; init; }

    public StoredFile(
        Guid id,
        string provider,
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string checksum,
        DateTimeOffset? missingSince,
        DateTimeOffset createdAt,
        Guid createdById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);

        Id = id;
        Provider = provider;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Checksum = checksum;
        MissingSince = missingSince;
        CreatedAt = createdAt;
        CreatedById = createdById;
    }
}

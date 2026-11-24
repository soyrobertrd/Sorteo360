using Sorteo360.Events.Core.Files;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Conversión entre la fila de <c>stored_file</c> y la entidad de dominio. Vive aparte porque la
/// escribe un repositorio y la lee otro: el archivo se da de alta junto con su enlace, en la
/// transacción del dueño.
/// </summary>
internal static class StoredFileMapper
{
    public static StoredFileData ToData(StoredFile file)
    {
        return new StoredFileData
        {
            Id           = file.Id,
            Provider     = file.Provider,
            StorageKey   = file.StorageKey,
            FileName     = file.FileName,
            ContentType  = file.ContentType,
            SizeBytes    = file.SizeBytes,
            Checksum     = file.Checksum,
            MissingSince = file.MissingSince,
            CreatedAt    = file.CreatedAt,
            CreatedById  = file.CreatedById
        };
    }

    public static StoredFile ToDomain(StoredFileData data)
    {
        return new StoredFile(
            id:           data.Id,
            provider:     data.Provider,
            storageKey:   data.StorageKey,
            fileName:     data.FileName,
            contentType:  data.ContentType,
            sizeBytes:    data.SizeBytes,
            checksum:     data.Checksum,
            missingSince: data.MissingSince,
            createdAt:    data.CreatedAt,
            createdById:  data.CreatedById);
    }
}

namespace Sorteo360.Platform.Files;

/// <summary>
/// Limpieza de bytes que ya nadie va a referenciar. Vive en el chasis porque el problema no es de
/// ningún dominio: cualquiera que guarde primero los bytes y después la fila que los enlaza tiene
/// que deshacer los bytes cuando la fila no llega a escribirse.
/// </summary>
public static class FileStoreCleanup
{
    /// <summary>
    /// Borra unos bytes cuyo enlace no llegó a escribirse. Va sin token de cancelación a
    /// propósito: si el cliente cortó, la limpieza igual tiene que correr. Que la limpieza falle
    /// no cambia lo que se le responde a quien subió el archivo, así que su avería no se propaga;
    /// lo que queda es un archivo huérfano, que la purga de retención barre.
    /// </summary>
    /// <param name="fileStore">Almacenamiento donde quedaron los bytes.</param>
    /// <param name="storageKey">Clave devuelta por <see cref="IFileStore.SaveAsync"/>.</param>
    public static async Task DiscardOrphanBytesAsync(this IFileStore fileStore, string storageKey)
    {
        ArgumentNullException.ThrowIfNull(fileStore);

        try
        {
            await fileStore.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (FileStoreException)
        {
        }
    }
}

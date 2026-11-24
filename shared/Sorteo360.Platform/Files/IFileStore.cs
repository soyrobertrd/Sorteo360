namespace Sorteo360.Platform.Files;

/// <summary>
/// El almacenamiento de archivos visto desde el dominio: guardar bytes, volver a leerlos y
/// borrarlos. Nada más.
/// <para>
/// <b>La frontera</b>: ninguna firma de acá nombra un proveedor, una ruta remota ni un
/// identificador de compartición. Una clave de almacenamiento es una cadena opaca que quien
/// llama guarda y devuelve tal cual, sin interpretarla nunca. Eso es lo que permite cambiar de
/// almacenamiento sin tocar una regla de negocio.
/// </para>
/// <para>
/// Los fallos viajan como excepciones y no como <c>Result</c>, igual que en los clientes
/// tipados del chasis: son averías de infraestructura, no reglas de negocio. Quien llama
/// distingue <see cref="FileNotFoundInStoreException"/> —el archivo ya no está— de
/// <see cref="FileStoreUnavailableException"/> —el almacenamiento no contesta—, que nunca
/// significan lo mismo.
/// </para>
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Guarda el contenido y devuelve dónde quedó. El nombre y el tipo son los del archivo
    /// original: un proveedor puede usarlos para darle forma legible a la clave, pero la clave
    /// que devuelve es la única autoridad sobre dónde están los bytes.
    /// </summary>
    /// <param name="content">Contenido a guardar; se lee una sola vez y desde su posición actual.</param>
    /// <param name="fileName">Nombre original del archivo, tal como lo envió quien lo subió.</param>
    /// <param name="contentType">Tipo de contenido del archivo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<StoredFileLocation> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Abre el contenido para leerlo en streaming. Quien llama es dueño del <see cref="Stream"/>
    /// devuelto y debe liberarlo.
    /// </summary>
    /// <param name="storageKey">Clave devuelta por <see cref="SaveAsync"/>.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Borra los bytes. Es idempotente: borrar lo que ya no está no es un fallo.
    /// <para>
    /// Existe desde el primer día aunque casi nada lo llame: es la operación en la que más se
    /// diferencian los proveedores, y descubrirlo al escribir la purga de retención obligaría a
    /// reabrir todos ellos.
    /// </para>
    /// </summary>
    /// <param name="storageKey">Clave devuelta por <see cref="SaveAsync"/>.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

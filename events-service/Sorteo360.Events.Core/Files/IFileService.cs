using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Files;

/// <summary>
/// Lectura de archivos por id, para cualquier consumidor que tenga uno: una foto de proveedor
/// hoy, un adjunto de cotización mañana. Nada de acá sabe para qué sirve el archivo.
/// </summary>
public interface IFileService
{
    /// <summary>Ficha de un archivo. <see cref="Maybe{T}.None"/> si el id no existe.</summary>
    Task<Result<Maybe<StoredFile>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Abre el contenido de un archivo. <see cref="Maybe{T}.None"/> si el id no existe;
    /// <c>FILE_MISSING</c> si la fila está pero el almacenamiento ya no tiene los bytes, y
    /// <c>FILE_STORE_UNAVAILABLE</c> si el almacenamiento no contesta. Los dos últimos no son lo
    /// mismo y nunca se confunden.
    /// <para>
    /// La primera lectura que encuentra el hueco es la que lo deja registrado en la ficha, y la
    /// primera que lo encuentra tapado es la que borra la marca. Un almacenamiento que no
    /// contesta no toca ninguna fila: no sabe nada del archivo, y en particular no sabe que
    /// falte.
    /// </para>
    /// </summary>
    Task<Result<Maybe<StoredFileContent>, BaseError>> OpenContentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista los archivos que el almacenamiento dejó de tener, del más reciente al más antiguo.
    /// Existe para que una persona pueda ir a buscarlos a la papelera del almacenamiento.
    /// </summary>
    Task<Result<PagedResult<StoredFile>, BaseError>> ListMissingAsync(int page, int pageSize, CancellationToken cancellationToken);
}

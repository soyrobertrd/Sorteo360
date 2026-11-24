using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Database;

/// <summary>
/// Contrato de persistencia de las fichas de archivo. Las filas que enlazan un archivo con
/// aquello que documenta las escribe el repositorio de su dueño, en la misma transacción.
/// </summary>
public interface IStoredFileRepository
{
    /// <summary>
    /// Obtiene la ficha de un archivo. Devuelve <see cref="Maybe{T}.None"/> cuando no existe.
    /// </summary>
    Task<Result<Maybe<StoredFile>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Sella la fecha desde la que el almacenamiento dice que el archivo ya no está. Solo la
    /// escribe si todavía no había ninguna: lo que hace falta conservar es el primer momento en
    /// que se detectó la ausencia, no el último intento de leerlo.
    /// </summary>
    Task<UnitResult<BaseError>> MarkMissingAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Borra la marca de ausencia porque los bytes volvieron a aparecer. Es lo que cierra el
    /// ciclo de una restauración desde la papelera del almacenamiento: sin esto la ficha
    /// quedaría marcada como perdida para siempre y el listado de faltantes dejaría de servir.
    /// </summary>
    Task<UnitResult<BaseError>> ClearMissingAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista las fichas marcadas como ausentes, de la más reciente a la más antigua. Es la
    /// consulta del operador: el nombre, quién lo subió y cuándo son lo que dice qué hay que
    /// restaurar desde la papelera del almacenamiento.
    /// </summary>
    Task<Result<PagedResult<StoredFile>, BaseError>> ListMissingAsync(int page, int pageSize, CancellationToken cancellationToken);
}

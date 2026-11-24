using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Files;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Database;

/// <summary>
/// Contrato de persistencia de las imágenes de un evento. Va aparte de
/// <see cref="IEventRepository"/> porque una imagen no participa del ciclo de vida del evento:
/// no la condiciona el estado ni sella nada.
/// <para>
/// Todo lo que devuelve <see cref="Maybe{T}.None"/> lo hace por la misma razón: el evento no
/// existe. La distinción entre "no hay evento" y "el evento no tiene imágenes" es la que separa
/// un 404 de una lista vacía.
/// </para>
/// </summary>
public interface IEventImageRepository
{
    /// <summary>
    /// Cuenta las imágenes vivas de un evento de ese tipo. <see cref="Maybe{T}.None"/> si el
    /// evento no existe, que es como se comprueba su existencia antes de tocar el
    /// almacenamiento.
    /// </summary>
    Task<Result<Maybe<int>, BaseError>> CountAsync(Guid eventId, EventImageKind kind, CancellationToken cancellationToken);

    /// <summary>
    /// Escribe la ficha del archivo y su enlace con el evento en una sola transacción.
    /// <c>false</c> si el evento dejó de existir entre la comprobación y la escritura, y en ese
    /// caso no se guarda ninguna de las dos filas.
    /// <para>
    /// Una portada reemplaza a la que hubiera: la anterior se da de baja en la misma transacción
    /// y sus bytes se quedan, igual que si se hubiera quitado a mano. Es lo que hace que
    /// "cambiar la portada" sea un clic y no dos, y que un fallo entre los dos no deje al evento
    /// sin ninguna.
    /// </para>
    /// </summary>
    Task<Result<bool, BaseError>> AddAsync(StoredFile file, EventImage image, CancellationToken cancellationToken);

    /// <summary>
    /// Las imágenes vivas de un evento con la ficha de su archivo, la portada primero y el resto
    /// del más antiguo al más reciente. <see cref="Maybe{T}.None"/> si el evento no existe.
    /// <para>
    /// <paramref name="kind"/> nulo trae las de los dos tipos, que es lo que pide la ficha del
    /// evento; con un tipo, solo las de ese.
    /// </para>
    /// </summary>
    Task<Result<Maybe<IReadOnlyList<EventImageWithFile>>, BaseError>> ListAsync(
        Guid eventId,
        EventImageKind? kind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Da de baja el enlace (soft-delete) sin tocar el archivo. <c>false</c> si la imagen no
    /// existe entre las de ese evento.
    /// </summary>
    Task<Result<bool, BaseError>> SoftDeleteAsync(
        Guid eventId,
        Guid imageId,
        Guid removedById,
        CancellationToken cancellationToken);
}

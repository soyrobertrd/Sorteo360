using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Las imágenes de un evento: la portada que lo encabeza y la galería con la que se promociona.
/// <para>
/// Vive fuera de <see cref="IEventService"/> a propósito. Ahí están las reglas del ciclo de vida
/// del evento —qué se puede editar en planning, qué sella el cierre— y ninguna aplica acá: una
/// foto documenta el evento, no lo cambia, así que sigue siendo legítimo subirla con el evento ya
/// iniciado.
/// </para>
/// </summary>
public interface IEventImageService
{
    /// <summary>
    /// Sube una imagen a un evento. <see cref="Maybe{T}.None"/> si el evento no existe. Todo lo
    /// que puede rechazar la subida se comprueba antes de escribir un byte.
    /// <para>
    /// Una portada reemplaza a la anterior, que queda desenlazada con sus bytes intactos.
    /// </para>
    /// </summary>
    Task<Result<Maybe<EventImageWithFile>, BaseError>> AddAsync(
        AddEventImageDto dto,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista las imágenes de un evento, opcionalmente solo las de un tipo.
    /// <see cref="Maybe{T}.None"/> si el evento no existe; una lista vacía si existe y no tiene
    /// ninguna que devolver.
    /// </summary>
    Task<Result<Maybe<IReadOnlyList<EventImageWithFile>>, BaseError>> ListAsync(
        Guid eventId,
        EventImageKind? kind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Quita una imagen. Solo da de baja el enlace: los bytes se quedan.
    /// </summary>
    Task<UnitResult<BaseError>> RemoveAsync(
        Guid eventId,
        Guid imageId,
        Guid removedById,
        CancellationToken cancellationToken);
}

using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>
/// La tabla <c>registration_window</c> (spec <c>06</c> §5.1). Toda lectura trata la fila ausente
/// como una ventana abierta, así que ningún llamador pregunta si existe.
/// </summary>
public interface IRegistrationWindowRepository
{
    /// <summary>La ventana del evento, abierta si no tiene fila.</summary>
    Task<Result<RegistrationWindow, BaseError>> GetAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// De esos eventos, los que tienen la ventana cerrada. Es el <i>left join</i> de la cartelera:
    /// una sola lectura por página en vez de una por evento.
    /// </summary>
    Task<Result<IReadOnlySet<Guid>, BaseError>> ListShutAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cierra la ventana, escribiendo la fila si es el primer cierre. <b>Cerrar una cerrada no
    /// cambia nada</b> (§5.2): ni la hora ni quién, que tienen un solo escritor cada uno.
    /// </summary>
    Task<Result<RegistrationWindow, BaseError>> CloseRegistrationsAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reabre la ventana: anula el cierre, conserva la fila y sella la reapertura. Reabrir una
    /// abierta no cambia nada, y sin fila no escribe ninguna.
    /// </summary>
    Task<Result<RegistrationWindow, BaseError>> ReopenRegistrationsAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken);
}

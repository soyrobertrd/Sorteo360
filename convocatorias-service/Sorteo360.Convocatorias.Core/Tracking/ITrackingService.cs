using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Tracking;

/// <summary>
/// El resumen de un evento: la pantalla que todos abren primero. <b>Todo es local</b> (§7): ni
/// events-service ni el CRM, así que un id que este servicio no conoce es un resumen en cero y con
/// la puerta abierta, nunca un <c>404</c> (§5).
/// </summary>
public interface ITrackingService
{
    /// <summary>
    /// El resumen del evento. Solo falla si falla la base propia; un evento sin nada es un resumen en
    /// cero.
    /// </summary>
    Task<Result<EventTracking, BaseError>> GetAsync(Guid eventId, CancellationToken cancellationToken);
}

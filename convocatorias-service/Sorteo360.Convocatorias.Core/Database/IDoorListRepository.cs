using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>La lectura de la lista de acceso (spec <c>09</c> §5.1): local, sin vecinos.</summary>
public interface IDoorListRepository
{
    /// <summary>Todas las inscripciones del evento, con lo justo para la puerta y para buscar.</summary>
    Task<Result<IReadOnlyList<DoorListRow>, BaseError>> ReadAsync(
        Guid eventId,
        CancellationToken cancellationToken);
}

using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>La lista de check-ins que lee la rifa (spec <c>09</c> §7): local, sin vecinos.</summary>
public interface ICheckInListRepository
{
    /// <summary>
    /// Toda inscripción del evento con check-in, sin filtro ni tope, por <c>checked_in_at</c> y
    /// después por id. Un evento que este servicio no conoce no es un error: no tiene filas.
    /// </summary>
    Task<Result<IReadOnlyList<CheckInListRow>, BaseError>> ReadAsync(
        Guid eventId,
        CancellationToken cancellationToken);
}

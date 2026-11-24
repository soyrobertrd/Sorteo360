using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <summary>
/// La lista de acceso de un evento (spec <c>09</c> §5.1). No es la lista de participantes de
/// <c>07</c>: no tiene invitados sin inscripción, ni estado de envío, ni correos.
/// </summary>
public interface IDoorListService
{
    /// <summary>
    /// Una página de las inscripciones del evento que pasan <paramref name="search"/> (ver
    /// <see cref="DoorListSearch"/>) y <paramref name="checkedIn"/>, por apellido, nombre y
    /// referencia, con los totales del evento entero. La página baja a 1 y el tamaño se topa entre
    /// 1 y <see cref="MaxPageSize"/>, como en las demás listas del servicio.
    /// </summary>
    Task<Result<DoorList, BaseError>> ListAsync(
        Guid eventId,
        string? search,
        bool? checkedIn,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 100;
}

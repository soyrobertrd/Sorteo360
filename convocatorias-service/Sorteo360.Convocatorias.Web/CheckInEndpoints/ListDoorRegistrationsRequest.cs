using Microsoft.AspNetCore.Mvc;

namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Búsqueda, filtro y paginación de la lista de acceso (spec <c>09</c> §5.1). Todas anulables y sin
/// inicializador de propiedad, como <c>ListParticipantsRequest</c>: con un <c>= 1</c> el enlace de
/// <c>[AsParameters]</c> falla y la lista entera responde <c>400</c>.
/// </summary>
public sealed record ListDoorRegistrationsRequest
{
    /// <summary>Palabras sobre el nombre, el apellido y la referencia, o la dirección entera y exacta.</summary>
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    /// <summary>Solo con check-in, o solo sin él.</summary>
    [FromQuery(Name = "checkedIn")]
    public bool? CheckedIn { get; init; }

    /// <summary>Página, 1-based. Por omisión, la primera.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>Tamaño de página. Por omisión 50, topado en 100.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
}

using Sorteo360.Convocatorias.Core.CheckIns;

namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// La lista de acceso (spec <c>09</c> §5.1): la página de <c>PagedResponse</c> con los totales del
/// evento al lado. <b>Sin correo</b>, en ninguna fila.
/// </summary>
public sealed record DoorListResponse
{
    /// <summary>Los del evento entero, sea cual sea la búsqueda: el pie del escáner.</summary>
    public required DoorListTotals Totals { get; init; }

    public required IReadOnlyList<DoorListItemResponse> Items { get; init; }

    /// <summary>Cuántas filas pasan la búsqueda y el filtro, no cuántas trae esta página.</summary>
    public required int Total { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

/// <summary>Una inscripción en la puerta: lo que se compara con un documento, y si ya entró.</summary>
public sealed record DoorListItemResponse
{
    public required Guid RegistrationId { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string? Company { get; init; }

    public required string Reference { get; init; }

    /// <summary>Cuándo entró, o nulo si todavía no.</summary>
    public required DateTimeOffset? CheckedInAt { get; init; }
}

namespace Sorteo360.Events.Web.EventEndpoints;

/// <summary>
/// Un evento de la lista de la puerta: lo que la terminal necesita para elegirlo y nada más
/// (spec <c>convocatorias/10</c> §4). Sin presupuesto, OPT, estado ni conteos: quien está en la
/// puerta ve el nombre del evento de esta noche, no el calendario. Las ciudades llevan la misma
/// forma que en el listado y el detalle.
/// </summary>
public record DoorEventResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public IReadOnlyList<CityResponse> Cities { get; init; } = [];
}

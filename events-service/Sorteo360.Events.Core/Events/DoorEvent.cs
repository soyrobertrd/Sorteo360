namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Un evento de la lista de la puerta, con sus ciudades: lo que una terminal necesita para elegir el
/// evento de esta noche (spec <c>convocatorias/10</c> §4). Sin imágenes, porque la terminal no las
/// muestra.
/// </summary>
public sealed record DoorEvent
{
    public Event Event { get; init; }

    /// <summary>Las ciudades del recorrido, ordenadas por display_order.</summary>
    public IReadOnlyList<RoadshowCity> Cities { get; init; }

    public DoorEvent(Event @event, IReadOnlyList<RoadshowCity> cities)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(cities);

        Event = @event;
        Cities = cities;
    }
}

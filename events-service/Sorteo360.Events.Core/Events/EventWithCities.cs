namespace Sorteo360.Events.Core.Events;

public sealed record EventWithCities
{
    public Event Event { get; init; }
    public IReadOnlyList<RoadshowCity> Cities { get; init; }

    /// <summary>
    /// Las imágenes vivas del evento, la portada primero. Viajan con el evento porque la ficha
    /// las muestra ahí mismo, y la página pública se arma con una sola lectura.
    /// </summary>
    public IReadOnlyList<EventImageWithFile> Images { get; init; }

    public EventWithCities(
        Event @event,
        IReadOnlyList<RoadshowCity> cities,
        IReadOnlyList<EventImageWithFile> images)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(images);

        Event = @event;
        Cities = cities;
        Images = images;
    }
}

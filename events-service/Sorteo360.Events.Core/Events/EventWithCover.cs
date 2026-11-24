namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Un evento con su portada y sus ciudades, que es la proyección que pide el listado.
/// <para>
/// Solo la portada y no la galería entera: una página de veinticinco eventos enseña
/// veinticinco imágenes, no todas las que cada evento acumuló. La galería se lee en el detalle.
/// </para>
/// <para>
/// Las ciudades sí viajan enteras, y en una sola consulta para la página completa. Son tres o
/// cuatro filas cortas por evento, y sin ellas una cartelera de roadshows no puede decir a dónde
/// va cada uno sin leer el detalle de cada fila.
/// </para>
/// </summary>
public sealed record EventWithCover
{
    public Event Event { get; init; }

    /// <summary>La portada del evento, o <c>null</c> si todavía no tiene una.</summary>
    public EventImageWithFile? Cover { get; init; }

    /// <summary>Las ciudades del recorrido, ordenadas por display_order.</summary>
    public IReadOnlyList<RoadshowCity> Cities { get; init; }

    public EventWithCover(Event @event, EventImageWithFile? cover, IReadOnlyList<RoadshowCity> cities)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(cities);

        Event = @event;
        Cover = cover;
        Cities = cities;
    }
}

namespace Sorteo360.Events.Web.EventEndpoints;

/// <summary>
/// Proyección liviana de un evento para listados: sin la galería y sin las imágenes de cada
/// ciudad, pero **con** las ciudades del recorrido, que son lo que identifica a un roadshow.
/// Reutiliza <see cref="OptRefResponse"/> y <see cref="MoneyResponse"/> de EventEndpoints para
/// que quien consume deserialice un solo tipo y no dos.
/// </summary>
public record EventListItemResponse
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Language { get; init; } = string.Empty;
    public string Visibility { get; init; } = string.Empty;

    /// <summary>Devuelve el montaje del salón del detalle, que es nulo si nadie lo eligió todavía.</summary>
    public string? SeatingLayout { get; init; }

    /// <summary>La misma del detalle. Un evento sin URL la devuelve nula, nunca como cadena vacía.</summary>
    public string? ExternalUrl { get; init; }

    public int? InvitedCount { get; init; }
    public MoneyResponse? TotalCost { get; init; }

    /// <summary>
    /// La portada del evento, o <c>null</c> si todavía no tiene una. Solo la portada: la galería
    /// se lee en el detalle, porque una página de veinticinco eventos enseña veinticinco
    /// imágenes y no todas las que cada uno acumuló.
    /// </summary>
    public EventImageResponse? Cover { get; init; }

    /// <summary>
    /// Las ciudades del recorrido, ordenadas, con la misma forma que en el detalle. Van acá
    /// porque un roadshow se identifica por dónde va: sin ellas una cartelera tendría que leer
    /// el detalle de cada fila para poder decirlo.
    /// </summary>
    public IReadOnlyList<CityResponse> Cities { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

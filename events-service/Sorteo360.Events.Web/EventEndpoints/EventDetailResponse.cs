using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Web.EventEndpoints;

public record EventDetailResponse
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string Visibility { get; init; } = string.Empty;

    /// <summary>Devuelve el montaje del salón, que es nulo si nadie lo eligió todavía.</summary>
    public string? SeatingLayout { get; init; }

    public string? ExternalUrl { get; init; }
    public string? Description { get; init; }
    public int? InvitedCount { get; init; }

    /// <summary>
    /// Lo presupuestado, escrito por la OPT. Va junto a <see cref="TotalCost"/>, que es lo
    /// realmente gastado según las cotizaciones aprobadas, y los dos pueden diferir sin que nada
    /// esté mal.
    /// </summary>
    public MoneyResponse? EstimatedBudget { get; init; }

    public MoneyResponse? TotalCost { get; init; }
    public string? CancellationReason { get; init; }
    public IReadOnlyList<CityResponse> Cities { get; init; } = [];

    /// <summary>
    /// Las imágenes del evento, la portada primero. Los bytes se piden aparte, a
    /// <c>/v1/files/{storedFileId}/content</c>; acá viaja lo que hace falta para reconocerlas.
    /// </summary>
    public IReadOnlyList<EventImageResponse> Images { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public record CityResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;

    /// <summary>Id IANA de la zona horaria de la ciudad, como <c>America/Chicago</c>.</summary>
    public string TimeZone { get; init; } = string.Empty;

    public DateOnly? Date { get; init; }
    public int? DisplayOrder { get; init; }

    /// <summary>
    /// La ciudad con la forma que comparten el detalle y el listado de eventos, la lista de la
    /// puerta y la respuesta del PATCH de la ciudad.
    /// </summary>
    public static CityResponse From(RoadshowCity city)
    {
        ArgumentNullException.ThrowIfNull(city);

        return new CityResponse
        {
            Id           = city.Id,
            Name         = city.Name,
            Country      = city.Country,
            TimeZone     = city.TimeZone,
            Date         = city.Date,
            DisplayOrder = city.DisplayOrder
        };
    }
}

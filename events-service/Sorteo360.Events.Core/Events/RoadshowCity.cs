namespace Sorteo360.Events.Core.Events;

public sealed record RoadshowCity
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string Name { get; init; }
    public string Country { get; init; }

    /// <summary>Id IANA de la zona horaria de la ciudad, ya validado por <see cref="CityTimeZone"/>.</summary>
    /// <remarks>
    /// Es el reloj en el que el acta notarial escribe cada momento del sorteo. Se guarda como texto y
    /// no como <see cref="CityTimeZone"/> a propósito: si una actualización de los datos de zonas
    /// dejara de reconocer el id, fallaría solo el acta y no cada lectura del evento.
    /// </remarks>
    public string TimeZone { get; init; }

    public DateOnly? Date { get; init; }
    public int? DisplayOrder { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }

    public RoadshowCity(
        Guid id,
        Guid eventId,
        string name,
        string country,
        string timeZone,
        DateOnly? date,
        int? displayOrder,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        Guid createdById,
        Guid updatedById,
        DateTimeOffset? deletedAt)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(country);
        ArgumentNullException.ThrowIfNull(timeZone);

        Id = id;
        EventId = eventId;
        Name = name;
        Country = country;
        TimeZone = timeZone;
        Date = date;
        DisplayOrder = displayOrder;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedById = createdById;
        UpdatedById = updatedById;
        DeletedAt = deletedAt;
    }
}

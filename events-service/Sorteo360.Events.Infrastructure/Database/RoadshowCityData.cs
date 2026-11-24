namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.roadshow_city</c>.
/// Solo se usa dentro de la capa de Infrastructure para interactuar con EF Core.
/// El repositorio la convierte hacia y desde las entidades de dominio.
/// </summary>
internal sealed class RoadshowCityData
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public Guid UpdatedById { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

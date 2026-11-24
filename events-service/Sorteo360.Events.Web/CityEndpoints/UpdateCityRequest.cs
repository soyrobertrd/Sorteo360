namespace Sorteo360.Events.Web.CityEndpoints;

public record UpdateCityRequest
{
    public string? Name { get; init; }
    public string? Country { get; init; }

    /// <summary>Id IANA de la zona horaria, como <c>America/Chicago</c>.</summary>
    /// <remarks>
    /// Es obligatorio, porque este PATCH reemplaza la ciudad completa (ADR-0009): omitirlo no conserva
    /// la zona guardada.
    /// </remarks>
    public string? TimeZone { get; init; }
    public DateOnly? Date { get; init; }
    public int? DisplayOrder { get; init; }
}

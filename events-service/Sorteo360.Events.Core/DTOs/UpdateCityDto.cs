using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Datos para editar la ciudad de un evento. Reemplaza los campos
/// mutables completos (nombre, pais, zona horaria, fecha, display order), igual que el PATCH
/// del evento. DTO de entrada: validado en el borde por
/// UpdateCityRequestValidator.
/// </summary>
public record UpdateCityDto(
    Guid EventId,
    Guid CityId,
    string Name,
    string Country,
    CityTimeZone TimeZone,
    DateOnly? Date,
    int? DisplayOrder,
    Guid UpdatedById);

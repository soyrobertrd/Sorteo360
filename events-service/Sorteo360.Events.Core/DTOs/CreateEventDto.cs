using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Datos para crear un evento y su primera ciudad. DTO de entrada: validado en
/// el borde por CreateEventRequestValidator; el agregado Evento protege sus
/// invariantes al construirse. Los ids de OPT se validan además contra el CRM
/// antes de escribir.
/// <para>
/// El idioma y la visibilidad llegan acá ya resueltos: los dos son opcionales en el cuerpo HTTP
/// —el formulario de eventos todavía no tiene control de visibilidad— y el borde pone <c>es</c> y
/// <c>private</c> cuando faltan. Cruzada esta frontera ya no existe un evento sin ninguno de los
/// dos.
/// </para>
/// </summary>
public record CreateEventDto(
    string Type,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Language,
    string Visibility,
    string? SeatingLayout,
    string? ExternalUrl,
    string? Description,
    int? InvitedCount,
    Money? EstimatedBudget,
    string CityName,
    string CityCountry,
    CityTimeZone CityTimeZone,
    Guid CreatedById);

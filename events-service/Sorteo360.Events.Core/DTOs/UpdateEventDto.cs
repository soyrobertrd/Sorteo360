using Sorteo360.Events.Core.Shared;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Datos para editar los campos mutables de un evento existente. El type se
/// fija al crear y el costo total se materializa al cerrar, por lo que ninguno
/// de los dos es editable. DTO de entrada: validado en el borde por
/// UpdateEventRequestValidator, y sus ids de OPT contra el CRM antes de escribir.
/// <para>
/// <c>Visibility</c> es el único campo que omitir no borra: nulo significa "dejala como está".
/// El formulario de eventos todavía no tiene control de visibilidad, así que tratarlo como los
/// demás campos opcionales despublicaría en silencio un evento anunciado cada vez que alguien le
/// corrige el nombre. <c>Language</c>, en cambio, sí es obligatorio, y el borde rechaza el nulo.
/// </para>
/// </summary>
public record UpdateEventDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Language,
    string? Visibility,
    string? SeatingLayout,
    string? ExternalUrl,
    string? Description,
    int? InvitedCount,
    Money? EstimatedBudget,
    Guid UpdatedById);

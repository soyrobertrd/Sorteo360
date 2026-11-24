using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Web.GlobalEndpoints;

namespace Sorteo360.Events.Web.EventEndpoints;

/// <summary>
/// Request enlazado desde la query string (con <c>[AsParameters]</c>) para el listado paginado
/// de eventos. Todos los filtros son opcionales; la paginación la hereda. Se mapea a
/// <c>ListEventsDto</c>.
/// <para>
/// El campo y el sentido del orden llegan como texto y no como enum: el binder de
/// <c>[AsParameters]</c> respondería 400 sin código de máquina ante un valor desconocido, y el
/// contrato de este listado exige uno. Los traduce el handler, que es quien puede rechazarlos con
/// su código.
/// </para>
/// </summary>
public record ListEventsRequest : PagingRequest
{
    [FromQuery] public string? Type { get; init; }
    [FromQuery] public string? Status { get; init; }
    [FromQuery] public bool? Active { get; init; }

    /// <summary>
    /// <c>public</c> o <c>private</c>. Es el filtro con el que el portal de convocatorias se queda
    /// solo con lo publicado; omitirlo trae ambas.
    /// </summary>
    [FromQuery] public string? Visibility { get; init; }
    [FromQuery] public DateOnly? StartDateFrom { get; init; }
    [FromQuery] public DateOnly? StartDateTo { get; init; }

    /// <summary>Texto a buscar en el nombre del evento. Vacío equivale a no buscar.</summary>
    [FromQuery] public string? SearchTerm { get; init; }

    [FromQuery] public string? SortField { get; init; }
    [FromQuery] public string? SortOrder { get; init; }
}

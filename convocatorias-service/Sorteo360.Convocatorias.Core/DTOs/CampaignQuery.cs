namespace Sorteo360.Convocatorias.Core.DTOs;

/// <summary>
/// Filtros y paginación del listado de campañas. El resumen por evento (spec <c>07</c> §2) lo
/// reusa con <c>LaunchedOnly</c> para su lista de campañas.
/// </summary>
/// <param name="Page">Página, 1-based.</param>
/// <param name="PageSize">Tamaño de página.</param>
/// <param name="EventId">Solo las de este evento.</param>
/// <param name="State">Solo las que están en este estado.</param>
/// <param name="LaunchedOnly">
/// Solo las que se lanzaron, que son las que cuentan en el resumen del evento (spec <c>07</c> §2).
/// No lo expone la API: lo pide el resumen.
/// </param>
public sealed record CampaignQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? EventId = null,
    string? State = null,
    bool LaunchedOnly = false);

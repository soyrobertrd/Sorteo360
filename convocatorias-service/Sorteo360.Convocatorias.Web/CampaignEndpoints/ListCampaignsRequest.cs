using Microsoft.AspNetCore.Mvc;

namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Filtros y paginación del listado de campañas. No es el resumen por evento, que es la spec
/// <c>07</c> §2.
/// <para>
/// Las cuatro son anulables y <b>ninguna lleva inicializador de propiedad</b>: los valores por
/// omisión los pone el manejador. No es estilo. Con un <c>= 1</c> en la propiedad, el enlace de
/// <c>[AsParameters]</c> falla y el listado entero responde <c>400</c> con el cuerpo vacío, con
/// o sin parámetros en la URL. Es la misma forma que ya tenía <c>ListTemplatesRequest</c>.
/// </para>
/// </summary>
public sealed record ListCampaignsRequest
{
    /// <summary>Página, 1-based. Por omisión, la primera.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>Tamaño de página. Por omisión 20, y el servicio lo topa en 100.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }

    /// <summary>Solo las campañas de este evento.</summary>
    [FromQuery(Name = "eventId")]
    public Guid? EventId { get; init; }

    /// <summary>Solo las que están en este estado.</summary>
    [FromQuery(Name = "state")]
    public string? State { get; init; }
}

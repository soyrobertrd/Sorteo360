using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Participants;

namespace Sorteo360.Convocatorias.Web.ParticipantEndpoints;

/// <summary>
/// Filtros y paginación de la lista de participantes (spec <c>07</c> §3.3). Todas anulables y sin
/// inicializador de propiedad, por lo mismo que <c>ListCampaignsRequest</c>: con un <c>= 1</c> el
/// enlace de <c>[AsParameters]</c> falla y la lista entera responde <c>400</c>.
/// <para>El CSV (§4) toma esta misma consulta e ignora la paginación: los mismos cinco filtros, no una copia.</para>
/// </summary>
public sealed record ListParticipantsRequest
{
    /// <summary>Página, 1-based. Por omisión, la primera.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>Tamaño de página. Por omisión 20, topado en 100.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }

    /// <summary><c>invited</c>, <c>referred</c> o <c>walk_in</c>.</summary>
    [FromQuery(Name = "origin")]
    public string? Origin { get; init; }

    /// <summary><c>handed_off</c>, <c>pending</c>, <c>failed</c>, <c>suppressed</c> o <c>none</c>.</summary>
    [FromQuery(Name = "sendStatus")]
    public string? SendStatus { get; init; }

    [FromQuery(Name = "registered")]
    public bool? Registered { get; init; }

    /// <summary>Subcadena sobre el nombre y la dirección.</summary>
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    /// <summary>Solo los destinatarios de esa campaña, borrador incluido.</summary>
    [FromQuery(Name = "campaignId")]
    public Guid? CampaignId { get; init; }

    /// <summary>Los cinco filtros, sin la paginación: lo que comparten la lista y el CSV.</summary>
    public ParticipantFilter ToFilter() => new(Origin, SendStatus, Registered, Search, CampaignId);
}

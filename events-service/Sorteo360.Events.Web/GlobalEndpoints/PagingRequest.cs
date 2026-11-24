using Microsoft.AspNetCore.Mvc;

namespace Sorteo360.Events.Web.GlobalEndpoints;

/// <summary>
/// La paginación que comparte todo listado: de qué página y de qué tamaño.
/// <para>
/// Los dos son nullable a propósito. El binder de <c>[AsParameters]</c> no respeta los
/// inicializadores de propiedad, así que un valor ausente llega como null y un 0 explícito llega
/// como 0: sin la distinción no habría forma de rechazar el segundo sin rechazar también el
/// primero. <see cref="ResolvedPage"/> y <see cref="ResolvedPageSize"/> son los que ya tienen el
/// default aplicado, y son los que se le pasan al servicio.
/// </para>
/// </summary>
public record PagingRequest
{
    [FromQuery] public int? Page { get; init; }
    [FromQuery] public int? PageSize { get; init; }

    /// <summary>Página pedida, o la primera si no vino ninguna.</summary>
    public int ResolvedPage => Page ?? PagingLimits.DefaultPage;

    /// <summary>Tamaño pedido, o el de referencia si no vino ninguno.</summary>
    public int ResolvedPageSize => PageSize ?? PagingLimits.DefaultPageSize;
}

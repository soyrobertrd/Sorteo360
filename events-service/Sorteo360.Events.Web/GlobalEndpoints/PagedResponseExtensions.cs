using Sorteo360.Events.Core.Shared;

namespace Sorteo360.Events.Web.GlobalEndpoints;

/// <summary>
/// El paso de una página del dominio a su página HTTP. Lo único que cambia entre un listado y
/// otro es cómo se ve un elemento; los tres números que lo rodean son siempre los mismos, y
/// copiarlos a mano en cada endpoint es como uno de ellos se queda sin copiar.
/// </summary>
public static class PagedResponseExtensions
{
    /// <param name="paged">Página tal como la devolvió el servicio.</param>
    /// <param name="mapItem">Cómo se ve un elemento en la respuesta.</param>
    public static PagedResponse<TResponse> ToResponse<T, TResponse>(
        this PagedResult<T> paged,
        Func<T, TResponse> mapItem)
    {
        ArgumentNullException.ThrowIfNull(paged);
        ArgumentNullException.ThrowIfNull(mapItem);

        return new PagedResponse<TResponse>
        {
            Total    = paged.Total,
            Page     = paged.Page,
            PageSize = paged.PageSize,
            Items    = [.. paged.Items.Select(mapItem)]
        };
    }
}

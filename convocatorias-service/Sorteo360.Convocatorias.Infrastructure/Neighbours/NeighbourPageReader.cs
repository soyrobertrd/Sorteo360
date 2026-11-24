using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Neighbours;

/// <summary>
/// La única forma de este servicio de leer una lista paginada de un vecino. Recorre las páginas,
/// decide cuándo la lista está entera y traduce la avería; lo que hay dentro de cada elemento no
/// le importa.
/// <para>
/// Existe porque la regla que gobierna esto —<b>una lista a medias nunca se devuelve como si
/// fuera la entera</b>— es la misma para el CRM y para events-service, y escrita dos veces se
/// aplicaría de dos maneras: el guard de cancelación se olvidó en la segunda copia y solo apareció
/// en revisión. El tercer vecino que llegue hereda estas decisiones en vez de volver a tomarlas.
/// </para>
/// </summary>
public static class NeighbourPageReader
{
    /// <summary>
    /// Lo que se pide por página. Los dos vecinos lo topan en 100, así que es su propio tope:
    /// pedir más no trae más, y pedir menos multiplica las llamadas de una lista larga.
    /// </summary>
    private const int PageSize = 100;

    /// <summary>
    /// Tope de páginas de una lectura. Cien páginas son diez mil elementos, muy por encima de
    /// cualquier selección o cartelera real. Existe para que un vecino que devolviera siempre una
    /// página llena no dejara esta petición girando para siempre.
    /// </summary>
    private const int MaxPages = 100;

    /// <summary>
    /// Trae todas las páginas de una lista de un vecino, o falla. Nunca devuelve una lista corta:
    /// o está entera, o es un <paramref name="unavailable"/>.
    /// </summary>
    /// <typeparam name="TPage">La página tal como la emite el vecino.</typeparam>
    /// <typeparam name="TItem">El elemento crudo de esa página; quien llama lo proyecta después.</typeparam>
    /// <param name="fetchPageAsync">Una llamada al vecino: página, tamaño y token.</param>
    /// <param name="itemsOf">Dónde están los elementos dentro de la página.</param>
    /// <param name="unavailable">
    /// El fallo que quiere levantar quien llama. El lector decide <b>cuándo</b> se falla; qué se le
    /// dice a cada público lo decide el llamante, y por eso <c>CRM_UNAVAILABLE</c> y
    /// <c>EVENTS_UNAVAILABLE</c> siguen siendo dos errores distintos.
    /// </param>
    /// <param name="logger">El registro de quien llama, para que la categoría siga siendo la suya.</param>
    /// <param name="neighbour">Cómo se nombra al vecino en el registro, p. ej. <c>events-service</c>.</param>
    /// <param name="listing">Qué se estaba listando, p. ej. <c>la cartelera pública</c>.</param>
    /// <param name="cancellationToken">Token de cancelación de quien llama.</param>
    public static Task<Result<IReadOnlyList<TItem>, BaseError>> ReadEveryPageAsync<TPage, TItem>(
        Func<int, int, CancellationToken, Task<TPage?>> fetchPageAsync,
        Func<TPage, IReadOnlyList<TItem>> itemsOf,
        BaseError unavailable,
        ILogger logger,
        string neighbour,
        string listing,
        CancellationToken cancellationToken)
        where TPage : class
    {
        ArgumentNullException.ThrowIfNull(fetchPageAsync);
        ArgumentNullException.ThrowIfNull(itemsOf);
        ArgumentNullException.ThrowIfNull(unavailable);
        ArgumentNullException.ThrowIfNull(logger);

        // La traducción de la avería es de NeighbourCall; acá solo queda cuándo la lista está
        // entera.
        return NeighbourCall.GuardAsync(
            async () =>
            {
                var items = new List<TItem>();

                for (var page = 1; page <= MaxPages; page++)
                {
                    var response = await fetchPageAsync(page, PageSize, cancellationToken);

                    // Un cuerpo vacío con 200 es un vecino que contestó sin decir nada. No es una
                    // lista vacía —eso viene con `items: []`— así que es un fallo y no un corte:
                    // media lista devuelta como si fuera la entera es la avería invisible.
                    if (response is null)
                    {
                        logger.LogError(
                            "{Neighbour} devolvió un cuerpo vacío en la página {Page} al listar {Listing}",
                            neighbour,
                            page,
                            listing);

                        return Result.Failure<IReadOnlyList<TItem>, BaseError>(unavailable);
                    }

                    var pageItems = itemsOf(response);
                    items.AddRange(pageItems);

                    // Página incompleta: se acabó el filtro, y la lista está entera.
                    if (pageItems.Count < PageSize)
                        return Result.Success<IReadOnlyList<TItem>, BaseError>(items);
                }

                // Se agotaron las páginas con la última todavía llena, así que hay más y no se
                // trajeron. Falla en vez de devolver una lista truncada que nadie podría distinguir
                // de la completa: quien espera 240 y recibe 227 en silencio no tiene forma de
                // encontrar a los 13 que faltan (spec 03 §6).
                logger.LogError(
                    "{Neighbour} superó el tope de {MaxPages} páginas al listar {Listing} ({Items} elementos)",
                    neighbour,
                    MaxPages,
                    listing,
                    items.Count);

                return Result.Failure<IReadOnlyList<TItem>, BaseError>(unavailable);
            },
            unavailable,
            logger,
            neighbour,
            "listar {Listing}",
            cancellationToken,
            listing);
    }
}

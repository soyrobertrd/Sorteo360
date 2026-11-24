namespace Sorteo360.Events.Web.GlobalEndpoints;

/// <summary>
/// Envoltura HTTP genérica para respuestas paginadas: el total de coincidencias, la
/// página y el tamaño de página actuales, y los elementos de la página. Reutilizable
/// por cualquier endpoint de listado.
/// </summary>
/// <typeparam name="T">Tipo de los elementos de la página.</typeparam>
public record PagedResponse<T>
{
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public IReadOnlyList<T> Items { get; init; } = [];
}

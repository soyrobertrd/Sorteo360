namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// La forma paginada que devuelven los listados de este servicio, igual que en los servicios
/// hermanos.
/// </summary>
/// <typeparam name="T">Tipo de los elementos de la página.</typeparam>
public sealed record PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Cuántos casan con el filtro en total, no cuántos trae esta página.</summary>
    public required int Total { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

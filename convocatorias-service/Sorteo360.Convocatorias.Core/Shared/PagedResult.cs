namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>
/// Resultado paginado genérico para la capa Core: la página de elementos más los metadatos de
/// paginación. La misma forma que declaran los servicios hermanos, para que una lista de este
/// servicio se lea igual que una de eventos o del CRM.
/// </summary>
/// <typeparam name="T">Tipo de los elementos de la página.</typeparam>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

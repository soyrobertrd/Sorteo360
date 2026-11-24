namespace Sorteo360.Notifications.Core.Shared;

/// <summary>
/// Resultado paginado genérico para la capa Core/Service: la página de elementos
/// más los metadatos de paginación (total de coincidencias, página y tamaño de página).
/// </summary>
/// <typeparam name="T">Tipo de los elementos de la página.</typeparam>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

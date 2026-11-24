using Sorteo360.Events.Web.GlobalEndpoints;

namespace Sorteo360.Events.Web.FileEndpoints;

/// <summary>
/// Request enlazado desde la query string (con <c>[AsParameters]</c>) para el listado de
/// archivos ausentes. No filtra por nada: la marca de ausencia ya es el filtro, así que lo único
/// que trae es la paginación que hereda.
/// </summary>
public record ListMissingFilesRequest : PagingRequest;

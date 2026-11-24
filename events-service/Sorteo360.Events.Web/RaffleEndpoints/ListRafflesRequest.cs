using Sorteo360.Events.Web.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Request enlazado desde la query string (con <c>[AsParameters]</c>) para el listado de sorteos de
/// todos los eventos. No filtra por nada: solo trae la paginación que hereda.
/// </summary>
public record ListRafflesRequest : PagingRequest;

namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Los campos por los que se puede ordenar el listado de eventos. Es un conjunto cerrado y no
/// una cadena libre: cada valor se traduce a una cláusula de orden escrita a mano, y un campo
/// que no esté acá no tiene ninguna. Por eso el borde rechaza lo que no reconoce en vez de
/// ignorarlo — devolver la página en otro orden del pedido es peor que decir que no.
/// <para>
/// Los nombres viajan por el query string tal cual, sin distinguir mayúsculas:
/// <c>name</c>, <c>startDate</c>, <c>status</c>, <c>totalCost</c>.
/// </para>
/// </summary>
public enum EventSortField
{
    Name,
    StartDate,
    Status,
    TotalCost
}

using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Filtros y orden para listar eventos de forma paginada. DTO de entrada: validado en el borde
/// por ListEventsRequestValidator; el <c>Type</c> y el <c>Status</c> llegan ya
/// canonicalizados (casing de almacenamiento) y los campos vacíos como <c>null</c>.
/// <para>
/// <c>SearchTerm</c> se compara contra el nombre del evento y nada más. El nombre de la OPT no
/// está en esta base —se resuelve al leer desde una copia del directorio del CRM que puede estar
/// vencida—, así que buscar contra él haría que los resultados se angostaran solos durante una
/// caída del CRM.
/// </para>
/// <para>
/// El orden no es opcional: sin <c>sortField</c> el borde pone el de siempre (fecha de inicio
/// descendente), así que acá siempre hay un campo y un sentido. Y son enums, no cadenas: un campo
/// de orden inválido deja de ser representable en cuanto se cruza esta frontera.
/// </para>
/// </summary>
public record ListEventsDto(
    string? Type,
    string? Status,
    bool? Active,
    string? Visibility,
    DateOnly? StartDateFrom,
    DateOnly? StartDateTo,
    string? SearchTerm,
    EventSortField SortField,
    SortOrder SortOrder,
    int Page,
    int PageSize);

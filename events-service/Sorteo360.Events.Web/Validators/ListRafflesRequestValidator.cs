using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>Solo la paginación: el listado de sorteos no admite ningún otro parámetro.</summary>
public sealed class ListRafflesRequestValidator : PagingRequestValidator<ListRafflesRequest>;

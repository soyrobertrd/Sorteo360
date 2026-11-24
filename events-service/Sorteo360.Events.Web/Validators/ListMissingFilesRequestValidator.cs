using Sorteo360.Events.Web.FileEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// Solo la paginación: el listado de ausentes no admite ningún otro parámetro.
/// </summary>
public sealed class ListMissingFilesRequestValidator : PagingRequestValidator<ListMissingFilesRequest>;

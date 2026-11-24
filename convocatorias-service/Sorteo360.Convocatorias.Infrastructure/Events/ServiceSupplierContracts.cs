namespace Sorteo360.Convocatorias.Infrastructure.Events;

/// <summary>El cuerpo de <c>POST /v1/service-suppliers</c> de events-service, tal como lo arma la carga de proveedores.</summary>
/// <remarks>
/// No lleva <c>services</c>: la carga no pide catálogo de servicios, y omitirlo es "sin servicios"
/// para events-service.
/// </remarks>
/// <param name="CompanyId">La empresa del CRM que se contrata en esta categoría.</param>
/// <param name="Category">La categoría principal.</param>
/// <param name="SecondaryCategories">Las otras categorías en las que también trabaja.</param>
/// <param name="AuditoriumCapacity">El aforo en auditorio; solo con Venue.</param>
/// <param name="CocktailCapacity">El aforo en cóctel; solo con Venue.</param>
/// <param name="Comments">Comentarios, hasta 2.000 caracteres.</param>
public sealed record EventsCreateServiceSupplierRequest(
    Guid CompanyId,
    string Category,
    IReadOnlyList<string> SecondaryCategories,
    int? AuditoriumCapacity,
    int? CocktailCapacity,
    string? Comments);

/// <summary>Una fila de <c>GET /v1/service-suppliers</c> de events-service, con solo lo que lee la carga.</summary>
/// <param name="Category">La categoría principal.</param>
/// <param name="SecondaryCategories">Las otras categorías; nulo se lee como ninguna.</param>
public sealed record EventsServiceSupplierListItem(string? Category, IReadOnlyList<string>? SecondaryCategories);

/// <summary>El ProblemDetails de un <c>409</c> de events-service, del que solo se lee el código.</summary>
/// <param name="Code">El código de máquina del rechazo.</param>
public sealed record EventsProblem(string? Code)
{
    /// <summary>La empresa ya es proveedor en esa categoría (<c>ServiceSupplierErrors.CompanyAlreadyRegistered</c>).</summary>
    public const string CompanyAlreadyRegisteredCode = "SUPPLIER_COMPANY_ALREADY_REGISTERED";
}

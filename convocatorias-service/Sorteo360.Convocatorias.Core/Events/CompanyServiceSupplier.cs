namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>Una ficha viva de proveedor de una empresa en events-service, con lo único que la carga lee de ella.</summary>
/// <param name="Category">La categoría principal.</param>
/// <param name="SecondaryCategories">Las otras categorías en las que también trabaja.</param>
public sealed record CompanyServiceSupplier(string Category, IReadOnlyList<string> SecondaryCategories)
{
    /// <summary>Todas sus categorías, la principal primero.</summary>
    public IEnumerable<string> AllCategories => [Category, .. SecondaryCategories];
}

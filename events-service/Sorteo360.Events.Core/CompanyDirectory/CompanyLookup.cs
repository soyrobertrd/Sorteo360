using Sorteo360.Events.Core.CrmDirectory;

namespace Sorteo360.Events.Core.CompanyDirectory;

/// <summary>
/// Las empresas resueltas para un conjunto de ids, con la confiabilidad de la resolución. Un id
/// ausente del diccionario devuelve <c>null</c>: es una identidad que no se pudo resolver, y quien
/// arma la respuesta lo emite tal cual en lugar de inventar un texto.
/// </summary>
public sealed record CompanyLookup(
    CrmDirectoryStatus Status,
    IReadOnlyDictionary<Guid, CompanySnapshot> Companies)
{
    /// <summary>Una resolución sin ninguna empresa, para cuando no hay copia del directorio.</summary>
    public static CompanyLookup Empty(CrmDirectoryStatus status) =>
        new(status, new Dictionary<Guid, CompanySnapshot>());

    /// <summary>La empresa resuelta, o <c>null</c> si no se resolvió.</summary>
    public CompanySnapshot? Snapshot(Guid companyId) => Companies.GetValueOrDefault(companyId);
}

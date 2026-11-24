using CSharpFunctionalExtensions;

namespace Sorteo360.Events.Core.CompanyDirectory;

/// <summary>
/// Los nombres de contactos del CRM, por id y en vivo. Lo usa el sorteo para nombrar al contacto
/// patrocinador de cada premio (spec 00 §8).
/// <para>
/// Va aparte de <see cref="ICompanyContactDirectory"/> porque responde otra pregunta: aquel trae a
/// todos los de una empresa para la ficha del proveedor; este nombra a unos pocos conocidos de
/// cualquier empresa, en una sola consulta para la respuesta entera. Tampoco hay copia en caché, por
/// la misma razón que allá: los contactos nunca se copian (ADR-0015).
/// </para>
/// </summary>
public interface IContactNameDirectory
{
    /// <summary>
    /// El nombre de cada contacto que el CRM conoce, o <c>None</c> si no contestó. Un id que el CRM no
    /// conoce simplemente no viene. No devuelve <c>Result</c>: es para mostrar, y una lectura nunca se
    /// cae por un nombre; que el CRM no contestara se informa en <c>CrmDirectoryStatusContext</c>.
    /// </summary>
    Task<Maybe<IReadOnlyDictionary<Guid, string>>> ResolveNamesAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken);
}

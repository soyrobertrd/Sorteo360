using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Crm;

/// <summary>
/// Los miembros de una lista de contactos del CRM, si la lista es de la OPT de quien llama
/// (PGTI-173). Este servicio lee la lista con su propia identidad, que la ve sin acotar por OPT,
/// así que la regla de "solo tu OPT" la aplica él, y la aplica acá.
/// </summary>
public interface IContactListReader
{
    /// <summary>
    /// Los ids de los miembros vivos de la lista, o un error:
    /// <list type="bullet">
    /// <item><c>403 OPT_NOT_ASSIGNED</c> si quien llama no trae OPT, sin preguntarle nada al CRM,
    /// o si su OPT está cerrada.</item>
    /// <item><c>404 CONTACT_LIST_NOT_FOUND</c> si la lista no existe o es de otra OPT.</item>
    /// <item><c>503 CRM_UNAVAILABLE</c> si el CRM no contesta: la selección no se degrada.</item>
    /// </list>
    /// </summary>
    /// <param name="contactListId">La lista elegida.</param>
    /// <param name="callerOptCode">El <c>opt_code</c> del token de quien llama; nulo si no trae.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<IReadOnlyList<Guid>, BaseError>> ReadOwnMemberIdsAsync(
        Guid contactListId,
        string? callerOptCode,
        CancellationToken cancellationToken);
}

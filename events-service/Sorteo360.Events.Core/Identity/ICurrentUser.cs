namespace Sorteo360.Events.Core.Identity;

/// <summary>
/// Usuario autenticado que ejecuta la petición actual. Es la fuente de los ids
/// de auditoría (<c>created_by_id</c> / <c>updated_by_id</c>) que se estampan
/// en cada escritura.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Id del usuario autenticado (claim <c>sub</c> del token).</summary>
    Guid Id { get; }
}

namespace Sorteo360.Convocatorias.Core.Identity;

/// <summary>
/// Quién hace la petición. Es el claim <c>sub</c> del token validado, un Guid pelado: la
/// identidad es de Keycloak, así que ninguna columna de este servicio la referencia con una
/// foreign key.
/// <para>
/// Lo necesita el lanzamiento, que estampa <c>launched_by</c>, y lo necesitan las columnas de
/// auditoría de <c>campaign</c>.
/// </para>
/// </summary>
public interface ICurrentUser
{
    /// <summary>Id del usuario autenticado. Lanza si la petición no trae uno válido.</summary>
    Guid Id { get; }

    /// <summary>
    /// Nombre para mostrar del usuario (claim <c>name</c>, o <c>preferred_username</c> si falta); nulo
    /// si el token no trae ninguno. Los enlaces de la carga de proveedores lo copian al crearlos y al
    /// desactivarlos, porque nada en la plataforma puede nombrar después a un usuario por su id.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// El código de su OPT, el claim <c>opt_code</c>; nulo si no trae, como un administrador
    /// central o una cuenta de servicio. Es un código y no una oficina resuelta: quien lo use lo
    /// compara con lo que diga el CRM.
    /// </summary>
    string? OptCode { get; }
}

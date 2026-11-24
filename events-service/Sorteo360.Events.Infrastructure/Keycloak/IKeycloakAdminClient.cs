namespace Sorteo360.Events.Infrastructure.Keycloak;

/// <summary>
/// Las lecturas de la Admin API de Keycloak que hacen falta para saber a quién avisarle: cuatro
/// para deducir quién puede aprobar, y una quinta para leer directo a quien sometió.
/// Es el único lugar de este servicio donde aparece una ruta del realm: el chasis sabe autenticarse
/// y sobrevivir a la red, pero no conoce ninguna ruta ni ningún DTO, y el dominio no sabe siquiera
/// que hay una red.
/// <para>
/// Todo fallo de red, de autenticación o de estado sale como <see cref="HttpRequestException"/>,
/// que es lo que el chasis garantiza. Quien llama decide la degradación; acá no hay política, solo
/// transporte.
/// </para>
/// <para>
/// <b>La cuenta de servicio necesita <c>view-realm</c> y <c>view-users</c></b>, y nada más.
/// <c>realm-admin</c> no, ni como atajo. <c>view-realm</c> es el que se olvida: la lectura de
/// miembros de un rol lo exige por dentro, y el nombre del endpoint no lo deja ver.
/// </para>
/// </summary>
public interface IKeycloakAdminClient
{
    /// <summary>
    /// Lista los roles del realm en representación breve, que es la que trae la marca de
    /// compuesto. Pagina de forma explícita: el endpoint devuelve 100 por defecto y no hay ningún
    /// endpoint que diga cuántos hay.
    /// </summary>
    Task<IReadOnlyList<KeycloakRoleResponse>> ListRealmRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve los roles de realm que <paramref name="roleName"/> contiene directamente. Es un
    /// solo nivel: la transitividad la arma quien llama, recorriendo el grafo.
    /// </summary>
    Task<IReadOnlyList<KeycloakRoleResponse>> GetRealmRoleCompositesAsync(
        string roleName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve las personas que tienen <paramref name="roleName"/> <b>asignado directamente</b>.
    /// <para>
    /// No expande compuestos, y por eso preguntar directamente por <c>quotes:approve</c> devuelve
    /// la lista vacía: ese rol nunca se asigna directo, es miembro de compuestos. No existe ningún
    /// endpoint que vaya de un rol a quienes lo tienen de verdad, así que el grafo se invierte
    /// aparte y acá se pregunta por cada ancestro.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<KeycloakUserResponse>> GetRealmRoleMembersAsync(
        string roleName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Busca personas por el valor de un atributo de usuario, p. ej. <c>opt_code=miami</c>.
    /// <para>
    /// Este es el único sitio donde el <c>opt_code</c> funciona: como filtro. Las respuestas no lo
    /// traen de vuelta (ver <see cref="KeycloakUserResponse"/>).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<KeycloakUserResponse>> FindUsersByAttributeAsync(
        string attribute,
        string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// La persona con ese id, o <c>null</c> si el realm contesta que no existe.
    /// <para>
    /// <b>Es la única lectura del grupo que no busca a nadie: ya sabe a quién quiere.</b> Quien
    /// sometió un intento viaja identificado en la fila, así que el grafo de roles y la
    /// intersección por <c>opt_code</c> no pintan nada acá —son la maquinaria de <i>deducir</i> un
    /// destinatario, y este no hay que deducirlo—.
    /// </para>
    /// <para>
    /// El <c>404</c> se traduce a <c>null</c> y no sale como excepción: una cuenta borrada es una
    /// respuesta del realm y no una avería suya. Todo lo demás sigue saliendo como
    /// <see cref="HttpRequestException"/>, igual que en las otras cuatro.
    /// </para>
    /// </summary>
    Task<KeycloakUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken);
}

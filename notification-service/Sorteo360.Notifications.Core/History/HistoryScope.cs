namespace Sorteo360.Notifications.Core.History;

/// <summary>
/// Hasta dónde llega una lectura del historial. Una persona con el rol de lectura lo ve todo; una
/// cuenta de servicio ve solo las filas de envíos que tratan de su propio servicio.
/// <para>
/// El alcance no nombra ningún rol compuesto ni distingue puestos: lo único que separa a las dos
/// lecturas es si quien llama es una cuenta de servicio, y eso lo dice el nombre de usuario que
/// Keycloak le da —<c>service-account-&lt;clientId&gt;</c>—, no un rol. Así, renombrar un puesto en
/// el realm no toca este código.
/// </para>
/// <para>
/// Acotar por OPT no se declinó, es inalcanzable: este servicio no conoce ningún <c>opt_code</c> y
/// no tiene cliente saliente para preguntarle a nadie si una persona puede ver un evento. Agregar
/// una columna de OPT acá devolvería justo el conocimiento que se le quitó a propósito.
/// </para>
/// </summary>
public sealed record HistoryScope
{
    /// <summary>Sufijo del <c>clientId</c> de un servicio, que no forma parte de su nombre corto.</summary>
    private const string ServiceClientSuffix = "-service";

    /// <summary>Prefijo con el que Keycloak nombra al usuario de una cuenta de servicio.</summary>
    private const string ServiceAccountPrefix = "service-account-";

    /// <summary>Sin límite: se ven las filas de todos los servicios.</summary>
    public static HistoryScope Everything { get; } = new((string?)null);

    /// <summary>
    /// El servicio al que se acota la lectura, o <c>null</c> si no se acota a ninguno.
    /// Se compara contra <c>notification_about.source_service</c>.
    /// </summary>
    public string? SourceService { get; }

    /// <summary>Si la lectura está acotada a un solo servicio.</summary>
    public bool IsScoped => SourceService is not null;

    private HistoryScope(string? sourceService) => SourceService = sourceService;

    /// <summary>
    /// El alcance de quien llama, a partir de su nombre de usuario.
    /// <para>
    /// Falla cerrado en los dos bordes: una cuenta de servicio cuyo cliente no siga la convención
    /// <c>&lt;nombre&gt;-service</c> queda acotada a su nombre tal cual, que no coincide con
    /// ninguna fila, así que ve una lista vacía en vez de verlo todo.
    /// </para>
    /// </summary>
    /// <param name="username">El <c>preferred_username</c> del token.</param>
    public static HistoryScope ForCaller(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Everything;

        if (!username.StartsWith(ServiceAccountPrefix, StringComparison.Ordinal))
            return Everything;

        var clientId = username[ServiceAccountPrefix.Length..];

        if (clientId.Length == 0)
            return Everything;

        var sourceService = clientId.EndsWith(ServiceClientSuffix, StringComparison.Ordinal)
            ? clientId[..^ServiceClientSuffix.Length]
            : clientId;

        return new HistoryScope(sourceService.Length == 0 ? clientId : sourceService);
    }
}

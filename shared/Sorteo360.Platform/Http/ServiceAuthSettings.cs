namespace Sorteo360.Platform.Http;

/// <summary>
/// Identidad con la que el servicio se presenta ante los demás servicios de la plataforma
/// (sección <c>ServiceAuth</c>): el cliente confidencial del realm cuyo secreto usa para
/// pedir su propio token por client credentials.
/// <para>
/// Va en una sección propia, y no bajo cada destino, porque la identidad es una sola
/// independientemente de a cuántos servicios llame. A quién llama es otra cosa y vive en
/// <c>Services:{Servicio}:BaseUrl</c>. El endpoint de token no se configura: se deriva de
/// <see cref="Auth.KeycloakSettings.Authority"/>, el mismo realm que ya valida los JWT
/// entrantes.
/// </para>
/// </summary>
public sealed record ServiceAuthSettings
{
    public static string SectionName => "ServiceAuth";

    /// <summary>
    /// <c>client_id</c> del cliente confidencial en el realm, ej. <c>events-service</c>.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// <c>client_secret</c> de ese cliente. Llega por variable de entorno desde el gestor
    /// de secretos; nunca se commitea ni se escribe en un log.
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;
}

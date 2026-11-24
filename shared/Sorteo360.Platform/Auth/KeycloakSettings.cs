namespace Sorteo360.Platform.Auth;

/// <summary>
/// Configuración de la validación de JWT emitidos por Keycloak (sección
/// <c>Keycloak</c>). <see cref="Authority"/> apunta al realm por el DNS interno
/// de la red (ej. <c>http://keycloak:8080/realms/pgti360</c>) y se usa para
/// descargar los metadatos OIDC y las claves de firma.
/// </summary>
public sealed record KeycloakSettings
{
    public static string SectionName => "Keycloak";

    /// <summary>URL del realm emisor; también es el issuer principal aceptado.</summary>
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// Audiencia (<c>aud</c>) que debe traer el token: el cliente-audiencia del
    /// servicio en el realm, ej. <c>events-api</c> o <c>crm-api</c>.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Exigir HTTPS al descargar los metadatos OIDC. Se apaga solo en local,
    /// donde Keycloak se sirve por HTTP dentro de la red de Compose.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// Issuer adicional aceptado. En local, los tokens pedidos desde el host
    /// llevan <c>iss</c> de <c>localhost:&lt;puerto&gt;</c> en lugar del DNS interno.
    /// </summary>
    public string? AdditionalValidIssuer { get; init; }
}

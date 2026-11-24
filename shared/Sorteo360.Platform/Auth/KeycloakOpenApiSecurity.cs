using Microsoft.OpenApi;

namespace Sorteo360.Platform.Auth;

/// <summary>
/// Describe en el documento OpenAPI el esquema de seguridad que el servicio ya exige en
/// tiempo de ejecución: un bearer JWT emitido por Keycloak. Sin esta descripción, Scalar
/// no muestra el panel de autenticación y toda prueba desde la UI responde 401.
/// Lo llama el document transformer de cada servicio, así que la definición vive una sola
/// vez aunque la consuman varios documentos.
/// </summary>
public static class KeycloakOpenApiSecurity
{
    /// <summary>
    /// Nombre del esquema en <c>components.securitySchemes</c>. Es el identificador que
    /// referencia el requisito de seguridad, no texto para mostrar.
    /// </summary>
    public const string SchemeName = "bearerAuth";

    private const string SchemeDescription =
        """
        Token de acceso emitido por Keycloak (realm `pgti360`). Pegar aquí solo el token,
        sin el prefijo `Bearer`: Scalar arma la cabecera `Authorization` por su cuenta y
        la reutiliza en todas las peticiones de esta pestaña.

        El token tiene que llevar en su audiencia la de este servicio. El cliente público
        `pgti360-app` ya la incluye, por lo que un token pedido con él sirve para probar
        aquí. Los endpoints de salud y esta misma documentación son anónimos y no lo piden.
        """;

    /// <summary>
    /// Añade el esquema bearer a los componentes del documento y lo exige de forma global.
    /// El requisito global es fiel al servicio: la fallback policy obliga a estar
    /// autenticado en todo endpoint sin <c>AllowAnonymous</c>, y los que sí lo son quedan
    /// fuera del documento.
    /// </summary>
    /// <param name="document">Documento OpenAPI que está componiendo el transformer.</param>
    public static void AddKeycloakBearerSecurity(this OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = SchemeDescription,
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });
    }
}

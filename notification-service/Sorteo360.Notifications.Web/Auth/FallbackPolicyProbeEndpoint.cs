using Microsoft.AspNetCore.Http;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Notifications.Web.Auth;

/// <summary>
/// Una ruta deliberadamente SIN <c>.RequireAuthorization</c>, y por eso mismo cerrada:
/// la fallback policy exige, en todo endpoint que no diga lo contrario, usuario autenticado
/// con <c>platform:staff</c> o <c>platform:service</c> (ADR-0025), así que sin token responde
/// 401 y sin ninguno de los dos roles 403, igual que cualquier otra.
/// <para>
/// Existe para que esa garantía sea comprobable en vez de declarada. Este servicio expone
/// el único endpoint anónimo de terceros de la plataforma (el webhook de SendGrid), así que
/// "un endpoint nuevo nace protegido" es la propiedad que más cuesta si algún día deja de
/// ser cierta, y un test HTTP contra esta ruta es lo que lo nota. No lleva datos, no toca
/// la base y queda fuera del documento OpenAPI.
/// </para>
/// </summary>
public sealed class FallbackPolicyProbeEndpoint : IEndpoint
{
    /// <summary>Ruta de la sonda. Fuera del prefijo versionado: no es parte de la API.</summary>
    public const string Route = "/_fallback-probe";

    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // No depende de nada.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        // Sin .RequireAuthorization y sin .AllowAnonymous: exactamente el caso que la
        // fallback policy tiene que cerrar. Cambiar cualquiera de las dos cosas invalida
        // la prueba.
        app.MapGet(Route, () => Results.NoContent())
            .ExcludeFromDescription();
    }
}

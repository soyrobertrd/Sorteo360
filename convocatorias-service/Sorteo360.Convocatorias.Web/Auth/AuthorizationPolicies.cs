using Microsoft.AspNetCore.Authorization;

namespace Sorteo360.Convocatorias.Web.Auth;

/// <summary>
/// Políticas de autorización de la API. Cada política exige un rol funcional
/// de Keycloak con el mismo nombre (los usuarios reciben roles compuestos por
/// puesto, como <c>opt_coordinator</c>, que Keycloak expande a estos roles finos
/// en el token; un compuesto cubre los servicios que ese puesto usa).
/// <para>
/// La lectura sí tiene rol propio, <c>convocatorias:read</c>, y es uno solo para todo el
/// servicio: una sola pantalla de seguimiento cruza campañas, registros y reconciliación,
/// así que un rol por recurso mentiría sobre su alcance.
/// </para>
/// <para>
/// Toda ruta interna tiene política propia; la fallback policy existe para la que se olvide de
/// tenerla, y pide <see cref="PlatformStaff"/> o <see cref="PlatformService"/> (ADR-0025).
/// </para>
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Armar la convocatoria: el borrador, los destinatarios, las subidas.</summary>
    public const string CampaignsWrite = "campaigns:write";

    /// <summary>
    /// Mandarla. <b>Dos roles y no uno</b>: <see cref="CampaignsWrite"/> arma y este lanza. Hoy
    /// los dos van en los tres compuestos, así que el día uno nadie queda acotado, y la
    /// separación existe para que acotar mañana sea un cambio de consola sin código detrás.
    /// Juntarlos ahora lo convertiría en un cambio de código, que es justo el intercambio que
    /// la división fino/compuesto de ADR-0010 existe para evitar.
    /// <para>
    /// Sustituye a <c>campaigns:submit</c> y <c>campaigns:approve</c>, que se escribieron en la
    /// spec <c>00</c> contra el plan anterior. El cliente retiró el paso de aprobación el
    /// 2026-09-07 (ADR-0017) y los cuatro actos —someter, retirar, aprobar, rechazar— se
    /// plegaron en uno: lanzar. Nada los reemplazó uno a uno.
    /// </para>
    /// </summary>
    public const string CampaignsLaunch = "campaigns:launch";

    public const string ConvocatoriasRead = "convocatorias:read";

    /// <summary>
    /// La puerta de un evento: escanear la entrada y deshacer un check-in equivocado (spec
    /// <c>09</c> §3 y §4). Va en los tres compuestos que hoy trabajan en la puerta. <b>La deshecha no
    /// tiene rol propio</b>: el error se comete en la puerta, el coordinador muchas veces no está, y
    /// quien acaba de equivocarse es quien mejor puede corregirlo.
    /// </summary>
    public const string CheckInWrite = "check-in:write";

    /// <summary>
    /// Leer la lista de acceso de la puerta (spec <c>09</c> §5.1): nombre, empresa, referencia y si
    /// entró, nunca un correo. Va en los mismos tres compuestos que <see cref="CheckInWrite"/>. No da
    /// <see cref="ConvocatoriasRead"/>, que abriría la lista de participantes con sus direcciones.
    /// </summary>
    public const string CheckInRead = "check-in:read";

    /// <summary>
    /// Leer la lista de check-ins de un evento desde la rifa (spec <c>09</c> §7): con correo,
    /// procedencia y contacto del CRM, sin filtro ni paginación. Solo lo lleva la cuenta de servicio
    /// de events-service, asignado directo; <b>ningún compuesto lo tiene</b>, así que ninguna persona
    /// llega a esta lista. Quien mira la puerta usa <see cref="CheckInRead"/>.
    /// </summary>
    public const string CheckInServiceRead = "check-in:service-read";

    /// <summary>
    /// Personal del MITUR: lo llevan los compuestos de puesto y abre toda ruta sin política propia.
    /// No es un rol funcional, así que no está en <see cref="All"/>. <c>event_staff</c> no lo lleva
    /// a propósito (ADR-0025): la puerta llega solo a lo que una política explícita le concede.
    /// </summary>
    public const string PlatformStaff = "platform:staff";

    /// <summary>
    /// Cuenta de servicio de la plataforma: la otra mitad de la fallback policy, asignada directo a
    /// cada cuenta y a ningún compuesto. No está en <see cref="All"/>, y <c>event_staff</c> tampoco
    /// lo lleva (ADR-0025).
    /// </summary>
    public const string PlatformService = "platform:service";

    /// <summary>Todos los roles funcionales (útil para tests y para registrar las políticas).</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        CampaignsWrite,
        CampaignsLaunch,
        ConvocatoriasRead,
        CheckInWrite,
        CheckInRead,
        CheckInServiceRead,
    ];

    /// <summary>
    /// Registra una política por rol funcional y la fallback policy, que en todo endpoint sin
    /// política explícita exige un usuario autenticado con <see cref="PlatformStaff"/> o
    /// <see cref="PlatformService"/> (sin token, 401; sin ninguno de los dos, 403). Es el
    /// único orden que falla cerrado: un endpoint nuevo queda protegido hasta que alguien
    /// decida lo contrario, en vez de quedar abierto hasta que alguien se acuerde. Fuera quedan
    /// los health checks y el prefijo público, vía <c>AllowAnonymous</c>.
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        var builder = services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(PlatformStaff, PlatformService)
                .Build());

        foreach (var role in All)
        {
            builder.AddPolicy(role, policy => policy.RequireRole(role));
        }

        return services;
    }
}

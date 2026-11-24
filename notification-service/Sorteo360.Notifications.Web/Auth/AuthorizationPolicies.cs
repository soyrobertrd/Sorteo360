using Microsoft.AspNetCore.Authorization;

namespace Sorteo360.Notifications.Web.Auth;

/// <summary>
/// Políticas de autorización de la API. Cada política exige un rol funcional de Keycloak
/// con el mismo nombre. Ningún nombre de rol compuesto aparece acá ni en ningún endpoint:
/// los compuestos se arman en el realm y renombrar un puesto no toca código (ADR-0010).
/// <para>
/// <c>notifications:send</c> NUNCA se le otorga a una persona. El endpoint de envío no
/// puede distinguir el token de una persona del de una máquina, así que sobre la fallback
/// policy cualquier usuario autenticado podría mandar correo a nombre del MITUR, desde su
/// dominio autenticado, con un efecto que sale de la plataforma y no se puede retractar.
/// La disciplina de compuestos es toda la barrera: se otorga directo a las cuentas de
/// servicio que llaman, y a ningún compuesto. Ver ADR-0020 y docs/specs/notifications/00.
/// </para>
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Enviar una notificación. Solo cuentas de servicio; nunca un compuesto humano.</summary>
    public const string NotificationsSend = "notifications:send";

    /// <summary>Leer el historial de envíos. Hoy solo lo lleva <c>central_admin</c>.</summary>
    public const string NotificationsRead = "notifications:read";

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
        NotificationsSend,
        NotificationsRead,
    ];

    /// <summary>
    /// Registra una política por rol funcional y la fallback policy, que en todo endpoint sin
    /// política explícita exige un usuario autenticado con <see cref="PlatformStaff"/> o
    /// <see cref="PlatformService"/> (sin token, 401; sin ninguno de los dos, 403). Es el
    /// único orden que falla cerrado: un endpoint nuevo queda protegido hasta que alguien
    /// decida lo contrario, en vez de quedar abierto hasta que alguien se acuerde. Fuera quedan
    /// los health checks y la documentación, vía <c>AllowAnonymous</c>.
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

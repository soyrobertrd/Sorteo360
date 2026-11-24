using Microsoft.AspNetCore.Authorization;

namespace Sorteo360.Events.Web.Auth;

/// <summary>
/// Políticas de autorización de la API. Cada política exige un rol funcional
/// de Keycloak con el mismo nombre (los usuarios reciben roles compuestos por
/// puesto, como <c>opt_coordinator</c>, que Keycloak expande a estos roles finos
/// en el token; un compuesto cubre los servicios que ese puesto usa).
/// Las lecturas no tienen política propia: la fallback policy exige, en todo endpoint
/// sin política explícita, un usuario autenticado con <see cref="PlatformStaff"/> o
/// <see cref="PlatformService"/> (ADR-0025).
/// </summary>
public static class AuthorizationPolicies
{
    public const string EventsWrite = "events:write";
    public const string EventsStart = "events:start";
    public const string EventsCancel = "events:cancel";
    public const string EventsClose = "events:close";
    public const string RafflesWrite = "raffles:write";
    public const string RafflesDraw = "raffles:draw";
    public const string FeatureSwitchesWrite = "feature-switches:write";
    public const string PlatformAdmin = "platform-admin";

    /// <summary>
    /// El rol es de convocatorias-service, pero la terminal de la puerta lo trae para elegir el
    /// evento de esta noche, y este servicio es el dueño de los eventos (spec <c>convocatorias/10</c> §4).
    /// </summary>
    public const string CheckInRead = "check-in:read";

    /// <summary>
    /// Personal autorizado de la plataforma: lo llevan los compuestos de puesto y abre toda ruta sin política propia.
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
        EventsWrite,
        EventsStart,
        EventsCancel,
        EventsClose,
        RafflesWrite,
        RafflesDraw,
        CheckInRead,
        FeatureSwitchesWrite,
        PlatformAdmin,
    ];

    /// <summary>
    /// Registra una política por rol funcional y la fallback policy, que en todo endpoint
    /// sin política explícita exige un usuario autenticado con <see cref="PlatformStaff"/> o
    /// <see cref="PlatformService"/>: sin token responde 401, y con un token sin ninguno de
    /// los dos, 403 (los health checks quedan fuera vía <c>AllowAnonymous</c>).
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

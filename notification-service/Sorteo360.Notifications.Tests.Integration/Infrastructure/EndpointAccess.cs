using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// A qué endpoints entra un juego de roles, decidido igual que lo decide el middleware de
/// autorización: la política propia del endpoint o, sin ella, la fallback policy, y nada para los
/// <c>AllowAnonymous</c>. Se recorre la tabla de rutas registrada, así que una ruta nueva entra sola
/// en la cuenta.
/// <para>
/// Uno por servicio a propósito: es un ayudante de test, no material del chassis.
/// </para>
/// </summary>
public static class EndpointAccess
{
    /// <summary>
    /// Los endpoints no anónimos que admiten a un usuario autenticado con <paramref name="roles"/>,
    /// cada uno por su nombre (<c>WithName</c>) o, si no tiene, por verbo y ruta.
    /// </summary>
    public static async Task<IReadOnlyList<string>> AuthorizedEndpointsAsync(
        IServiceProvider services,
        IEnumerable<string> roles)
    {
        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            roles.Select(role => new Claim("roles", role)),
            TestAuthHandler.SchemeName,
            nameType: "preferred_username",
            roleType: "roles"));

        var authorized = new List<string>();

        foreach (var endpoint in services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                continue;

            // Lo mismo que combina AuthorizationMiddleware; sin datos de autorización devuelve la fallback.
            var policy = await AuthorizationPolicy.CombineAsync(
                policyProvider,
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());

            // Y, como el middleware, los requisitos que un endpoint declara sueltos.
            var requirements = endpoint.Metadata.GetOrderedMetadata<IAuthorizationRequirementData>()
                .SelectMany(data => data.GetRequirements())
                .ToArray();
            if (requirements.Length > 0)
            {
                var declared = new AuthorizationPolicyBuilder().AddRequirements(requirements).Build();
                policy = policy is null ? declared : AuthorizationPolicy.Combine(policy, declared);
            }

            if (policy is null || (await authorization.AuthorizeAsync(user, endpoint, policy)).Succeeded)
                authorized.Add(Describe(endpoint));
        }

        return authorized;
    }

    private static string Describe(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
        ?? $"{string.Join(",", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}";
}

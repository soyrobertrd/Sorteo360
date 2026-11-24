using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Web.Auth;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// Esquema de autenticación de prueba: reemplaza al JWT de Keycloak en los tests
/// de integración. Por defecto autentica cada petición como un usuario con todos
/// los roles funcionales más <c>platform:staff</c>, que pide la fallback policy (así los
/// tests de comportamiento no cambian); cabeceras
/// especiales permiten simular una petición anónima o acotar roles y usuario:
/// <list type="bullet">
/// <item><c>X-Test-Anonymous</c>: la petición no se autentica (espera 401).</item>
/// <item><c>X-Test-Roles</c>: roles separados por coma en lugar del set completo. Un test que
/// acota roles y pega en una ruta sin política propia agrega <c>platform:staff</c>, o
/// <c>platform:service</c> si simula a un servicio.</item>
/// <item><c>X-Test-Sub</c>: id (guid) del usuario en lugar del predeterminado.</item>
/// </list>
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string AnonymousHeader = "X-Test-Anonymous";
    public const string RolesHeader = "X-Test-Roles";
    public const string SubHeader = "X-Test-Sub";
    public const string AccountIdHeader = "X-Test-Account-Id";

    /// <summary>
    /// Lo que trae en <c>roles</c> el token de quien está en la puerta: Keycloak expande el compuesto
    /// <c>event_staff</c> y pone su nombre y cada rol que lleva. Va tal cual en <see cref="RolesHeader"/>.
    /// </summary>
    public const string DoorStaffRoles = "event_staff,check-in:write,check-in:read";

    /// <summary>Id del usuario de prueba predeterminado (estable para asserts de auditoría).</summary>
    public static readonly Guid DefaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(AnonymousHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var sub = Request.Headers.TryGetValue(SubHeader, out var subValues)
            ? subValues.ToString()
            : DefaultUserId.ToString();

        var roles = Request.Headers.TryGetValue(RolesHeader, out var rolesValues)
            ? rolesValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [.. AuthorizationPolicies.All, AuthorizationPolicies.PlatformStaff];

        // Mismos nombres de claim que emite Keycloak (MapInboundClaims = false).
        var claims = new List<Claim>
        {
            new("sub", sub),
            new("preferred_username", "test-user"),
        };

        if (Request.Headers.TryGetValue(AccountIdHeader, out var accountValues))
        {
            var rawAccount = accountValues.ToString();
            if (!string.IsNullOrWhiteSpace(rawAccount) && !string.Equals(rawAccount, "none", StringComparison.OrdinalIgnoreCase))
                claims.Add(new("account_id", rawAccount));
        }
        else
        {
            claims.Add(new("account_id", Account.DefaultBanreservasAccountId.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim("roles", role)));

        var identity = new ClaimsIdentity(claims, SchemeName, nameType: "preferred_username", roleType: "roles");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Registro del esquema de prueba como predeterminado en las factories.</summary>
public static class TestAuthenticationExtensions
{
    /// <summary>
    /// Registra <see cref="TestAuthHandler"/> y lo fija como esquema predeterminado,
    /// desplazando al JWT bearer que registra la aplicación.
    /// </summary>
    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        return services;
    }
}

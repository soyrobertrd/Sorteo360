using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Auth;

/// <summary>
/// Cobertura de autenticación y autorización sobre el pipeline real: peticiones
/// anónimas reciben 401 en todos los grupos de endpoints, roles insuficientes
/// reciben 403 en los endpoints protegidos, y el rol correcto pasa la
/// autorización. No requiere base de datos: la respuesta se decide antes del handler.
/// </summary>
public sealed class AuthorizationTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public AuthorizationTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    #region Anónimo -> 401 en todos los grupos

    [Theory]
    [InlineData("GET", "v1/events")]
    [InlineData("GET", "v1/events/door")]
    [InlineData("POST", "v1/events")]
    [InlineData("GET", "v1/events/6a4a1a1e-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "v1/events/6a4a1a1e-0000-0000-0000-000000000001")]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/start")]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/cancel")]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/close")]
    [InlineData("PATCH", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/cities/6a4a1a1e-0000-0000-0000-000000000002")]
    [InlineData("GET", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/raffle")]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/raffle")]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/raffle/start")]
    public async Task Request_Returns401_WhenAnonymous(string method, string route)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/_alive")]
    [InlineData("/_ready")]
    public async Task HealthEndpoints_StayAnonymous_WhenNoTokenIsSent(string route)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Rol insuficiente -> 403

    [Theory]
    // Escrituras: un rol de otro recurso no alcanza.
    [InlineData("POST", "v1/events", AuthorizationPolicies.RafflesWrite)]
    [InlineData("PATCH", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/cities/6a4a1a1e-0000-0000-0000-000000000002", AuthorizationPolicies.RafflesWrite)]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/raffle", AuthorizationPolicies.EventsWrite)]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/raffle/start", AuthorizationPolicies.RafflesWrite)]
    // Las transiciones tienen permiso propio: editar no habilita cerrar/cancel.
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/close", AuthorizationPolicies.EventsWrite)]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/cancel", AuthorizationPolicies.EventsStart)]
    // La lista de la puerta tiene política propia, así que platform:staff solo no la abre: la
    // fallback no cuenta en una ruta que declara la suya.
    [InlineData("GET", "v1/events/door", AuthorizationPolicies.PlatformStaff)]
    public async Task Request_Returns403_WhenRoleIsInsufficient(string method, string route, string grantedRole)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, grantedRole);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Una lectura sin política propia cae en la fallback, y la fallback pide <c>platform:staff</c> o
    /// <c>platform:service</c> (ADR-0025): un rol funcional solo, incluso uno de escritura, no alcanza.
    /// </summary>
    [Theory]
    [InlineData("v1/events", AuthorizationPolicies.EventsWrite)]
    [InlineData("v1/events/6a4a1a1e-0000-0000-0000-000000000001", "check-in:write")]
    [InlineData("v1/events/6a4a1a1e-0000-0000-0000-000000000001/raffle", AuthorizationPolicies.RafflesWrite)]
    public async Task FallbackRoute_Returns403_WhenTheCallerHoldsNoPlatformRole(string route, string grantedRole)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, grantedRole);

        var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Quien está en la puerta lee la lista de eventos de la puerta y nada más de este servicio
    /// (spec <c>convocatorias/10</c>, definición de terminado).
    /// </summary>
    [Theory]
    [InlineData("v1/events")]
    [InlineData("v1/prize-draws/by-code/ABC123")]
    public async Task Read_Returns403_WhenTheCallerIsDoorStaff(string route)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, TestAuthHandler.DoorStaffRoles);

        var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Registro de políticas

    [Fact]
    public async Task FallbackPolicy_DemandsAnAuthenticatedUserHoldingAPlatformRole_WhenTheServiceIsBuilt()
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();

        fallback.ShouldNotBeNull();
        fallback.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().ShouldNotBeEmpty();
        fallback.Requirements
            .OfType<RolesAuthorizationRequirement>()
            .ShouldHaveSingleItem()
            .AllowedRoles.ShouldBe([AuthorizationPolicies.PlatformStaff, AuthorizationPolicies.PlatformService], ignoreOrder: true);
    }

    [Fact]
    public void All_ReturnsNoPlatformRole_WhenRead()
    {
        // Los dos roles de plataforma no son roles funcionales: no tienen política propia.
        AuthorizationPolicies.All.ShouldNotContain(AuthorizationPolicies.PlatformStaff);
        AuthorizationPolicies.All.ShouldNotContain(AuthorizationPolicies.PlatformService);
    }

    [Fact]
    public async Task AllFunctionRoles_HaveARegisteredPolicyDemandingThatSameRole_WhenTheServiceIsBuilt()
    {
        // Cubre también los roles cuyo endpoint aún no existe: la política queda
        // registrada y exigible desde que el rol entra en la lista.
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var role in AuthorizationPolicies.All)
        {
            var policy = await provider.GetPolicyAsync(role);

            policy.ShouldNotBeNull($"la política '{role}' no está registrada");
            policy.Requirements
                .OfType<RolesAuthorizationRequirement>()
                .ShouldContain(requirement => requirement.AllowedRoles.Contains(role));
        }
    }

    #endregion

    #region Rol correcto -> pasa la autorización

    [Theory]
    [InlineData("POST", "v1/events", AuthorizationPolicies.EventsWrite)]
    [InlineData("POST", "v1/events/6a4a1a1e-0000-0000-0000-000000000001/cancel", AuthorizationPolicies.EventsCancel)]
    public async Task Request_PassesAuthorization_WhenRoleMatches(string method, string route, string grantedRole)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, grantedRole);

        // Cuerpo vacío a propósito: la autorización pasa y el rechazo (400) viene
        // de la validación del payload, no del pipeline de seguridad.
        var request = new HttpRequestMessage(new HttpMethod(method), route)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion
}

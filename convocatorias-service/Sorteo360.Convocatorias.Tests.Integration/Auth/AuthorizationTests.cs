using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Auth;

/// <summary>
/// La política por defecto del servicio: sin usuario autenticado no se entra a ningún lado
/// salvo a los health checks, a la documentación y al prefijo público.
/// <para>
/// La fallback policy se comprueba sobre la configuración registrada y además por HTTP: ya hay
/// endpoints de dominio a los que apuntar, así que un 401 de verdad prueba lo que antes solo
/// decía la configuración. El reparto de lo que está mapeado lo vigila
/// <see cref="PublicSurfaceGuardTests"/>.
/// </para>
/// </summary>
public sealed class AuthorizationTests : IClassFixture<ConvocatoriasApiFactory>
{
    private readonly ConvocatoriasApiFactory _factory;

    public AuthorizationTests(ConvocatoriasApiFactory factory) => _factory = factory;

    [Fact]
    public async Task FallbackPolicy_ReturnsARequirementForAnAuthenticatedUserHoldingAPlatformRole_WhenAnEndpointHasNoPolicy()
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

    /// <summary>
    /// Toda ruta interna de este servicio tiene política propia, así que ninguna llega a la fallback
    /// por HTTP. La fallback se evalúa entonces directo contra un usuario: es la que cerraría una ruta
    /// nueva que olvidara su política, y tiene que cerrarla también para la puerta (ADR-0025).
    /// </summary>
    [Theory]
    [InlineData(AuthorizationPolicies.PlatformStaff, true)]
    [InlineData(AuthorizationPolicies.PlatformService, true)]
    [InlineData(AuthorizationPolicies.CheckInWrite, false)]
    [InlineData(AuthorizationPolicies.ConvocatoriasRead, false)]
    public async Task FallbackPolicy_SucceedsOnlyForAPlatformRole_WhenEvaluatedAgainstAUser(string role, bool expected)
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = _factory.Services.GetRequiredService<IAuthorizationService>();
        var fallback = await provider.GetFallbackPolicyAsync();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", TestAuthHandler.DefaultUserId.ToString()), new Claim("roles", role)],
            TestAuthHandler.SchemeName, nameType: "sub", roleType: "roles"));

        var result = await authorization.AuthorizeAsync(user, fallback!);

        result.Succeeded.ShouldBe(expected);
    }

    [Fact]
    public async Task FallbackPolicy_Fails_WhenEvaluatedAgainstAnAnonymousUser()
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = _factory.Services.GetRequiredService<IAuthorizationService>();
        var fallback = await provider.GetFallbackPolicyAsync();

        var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), fallback!);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void AllPolicies_ReturnsNoPlatformRole_WhenRead()
    {
        // Los dos roles de plataforma no son roles funcionales: no tienen política propia.
        AuthorizationPolicies.All.ShouldNotContain(AuthorizationPolicies.PlatformStaff);
        AuthorizationPolicies.All.ShouldNotContain(AuthorizationPolicies.PlatformService);
    }

    [Theory]
    [InlineData(AuthorizationPolicies.CampaignsWrite)]
    [InlineData(AuthorizationPolicies.CampaignsLaunch)]
    [InlineData(AuthorizationPolicies.ConvocatoriasRead)]
    public async Task GetPolicy_ReturnsAPolicyDemandingExactlyThatRole_WhenAskedForAFunctionalRole(string role)
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync(role);

        policy.ShouldNotBeNull();
        policy.Requirements
            .OfType<RolesAuthorizationRequirement>()
            .SelectMany(requirement => requirement.AllowedRoles)
            .ShouldBe([role]);
    }

    /// <summary>
    /// Los dos roles de la época de la aprobación se fueron con ella. Se comprueba como
    /// ausencia y no solo por la lista de arriba: una política que sigue registrada es una que
    /// un endpoint puede seguir exigiendo, y el realm ya no la concede a nadie.
    /// </summary>
    [Theory]
    [InlineData("campaigns:submit")]
    [InlineData("campaigns:approve")]
    public async Task GetPolicy_ReturnsNull_WhenAskedForARetiredApprovalRole(string retiredRole)
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync(retiredRole);

        policy.ShouldBeNull();
        AuthorizationPolicies.All.ShouldNotContain(retiredRole);
    }

    /// <summary>
    /// El servicio tiene exactamente estos siete roles. Un octavo que apareciera sin pasar por
    /// una edición del realm rompería acá, que es lo que mantiene el código y el realm
    /// hablando de lo mismo.
    /// </summary>
    [Fact]
    public void AllPolicies_ReturnsTheSevenFunctionalRoles_WhenRead()
    {
        AuthorizationPolicies.All.ShouldBe(
        [
            "campaigns:write",
            "campaigns:launch",
            "convocatorias:read",
            "check-in:write",
            "check-in:read",
            "check-in:service-read",
            "supplier-intake:manage",
        ]);
    }

    [Theory]
    [InlineData("/_alive")]
    [InlineData("/_ready")]
    [InlineData("/_health/all")]
    public async Task HealthEndpoints_Returns200_WhenCalledWithoutAToken(string path)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task ApiDocs_Returns200_WhenCalledWithoutAToken(string path)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

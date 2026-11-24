using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Sorteo360.Notifications.Web.Auth;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Auth;

/// <summary>
/// La política por defecto del servicio: sin usuario autenticado no se entra a ningún lado
/// salvo a los health checks y a la documentación, y autenticado hace falta además
/// <c>platform:staff</c> o <c>platform:service</c> (ADR-0025).
/// <para>
/// La prueba que importa es la de <see cref="FallbackPolicyProbeEndpoint"/>: una ruta real,
/// mapeada sin <c>.RequireAuthorization</c>, que igual responde 401. Comprobar la policy
/// registrada en el contenedor diría que está configurada; recorrerla por HTTP dice que
/// además se aplica.
/// </para>
/// </summary>
public sealed class AuthorizationTests : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory _factory;

    public AuthorizationTests(NotificationsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UnmarkedRoute_Returns401_WhenCalledWithoutAToken()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, FallbackPolicyProbeEndpoint.Route);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(AuthorizationPolicies.PlatformStaff)]
    [InlineData(AuthorizationPolicies.PlatformService)]
    public async Task UnmarkedRoute_Returns204_WhenTheCallerHoldsAPlatformRole(string role)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, FallbackPolicyProbeEndpoint.Route);
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Autenticado no alcanza (ADR-0025): sin <c>platform:staff</c> ni <c>platform:service</c>, ni
    /// siquiera los roles propios de este servicio abren una ruta sin política.
    /// </summary>
    [Theory]
    [InlineData(AuthorizationPolicies.NotificationsSend)]
    [InlineData(AuthorizationPolicies.NotificationsRead)]
    [InlineData("check-in:write")]
    public async Task UnmarkedRoute_Returns403_WhenTheCallerHoldsNoPlatformRole(string role)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, FallbackPolicyProbeEndpoint.Route);
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FallbackPolicy_ReturnsAPolicyThatDemandsAnAuthenticatedUserHoldingAPlatformRole_WhenTheEndpointHasNoPolicyOfItsOwn()
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

    [Theory]
    [InlineData(AuthorizationPolicies.NotificationsSend)]
    [InlineData(AuthorizationPolicies.NotificationsRead)]
    public async Task RolePolicies_ReturnsAPolicyDemandingThatOneRole_WhenLookedUpByRole(string role)
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync(role);

        policy.ShouldNotBeNull();
        policy.Requirements
            .OfType<RolesAuthorizationRequirement>()
            .SelectMany(requirement => requirement.AllowedRoles)
            .ShouldBe([role]);
    }

    [Theory]
    [InlineData("/_alive")]
    [InlineData("/_ready")]
    [InlineData("/_health/all")]
    public async Task HealthEndpoints_Returns200_WhenCalledAnonymously(string path)
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
    public async Task ApiDocs_ReturnsTheDocument_WhenCalledWithoutAToken(string path)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

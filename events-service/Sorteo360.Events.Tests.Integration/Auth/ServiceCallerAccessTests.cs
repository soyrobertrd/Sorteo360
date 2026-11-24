using System.Net;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Auth;

/// <summary>
/// Las rutas de este servicio que otro servicio llama hoy y que no tienen política propia
/// (ADR-0025): convocatorias-service lee los eventos.
/// <para>
/// <c>TestAuthHandler</c> concede lo que el test le pide, así que una cuenta de servicio sin
/// <c>platform:service</c> en el realm pasaría todo esto en verde. Lo que sí fija es el contrato:
/// con <c>platform:service</c> solo, la llamada entra; con lo que lleva la puerta
/// (<see cref="TestAuthHandler.DoorStaffRoles"/>), no. Corre con base de datos porque "entra"
/// quiere decir que el handler responde, no solo que la autorización no cortó.
/// </para>
/// </summary>
public sealed class ServiceCallerAccessTests : IClassFixture<EventsApiDbFactory>
{
    private readonly EventsApiDbFactory _factory;

    public ServiceCallerAccessTests(EventsApiDbFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string> CalledByOtherServices =>
    [
        "v1/events?status=planning&page=1&pageSize=20",
        "v1/events/6a4a1a1e-0000-0000-0000-000000000001",
    ];

    [Theory]
    [MemberData(nameof(CalledByOtherServices))]
    public async Task Route_PassesAuthorization_WhenTheCallerHoldsOnlyPlatformService(string route)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.PlatformService);

        var response = await client.GetAsync(route);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(CalledByOtherServices))]
    public async Task Route_Returns403_WhenTheCallerIsDoorStaff(string route)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, TestAuthHandler.DoorStaffRoles);

        var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

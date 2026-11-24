using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Auth;

/// <summary>
/// Quien está en la puerta llega a una sola ruta de este servicio, la lista de eventos de la puerta
/// (spec <c>convocatorias/10</c> §4), por su política <c>check-in:read</c>. A todo lo demás no
/// (spec <c>09</c> §8.1, ADR-0025): su token no lleva <c>platform:staff</c>, así que la fallback
/// policy lo deja fuera. Se recorre cada endpoint mapeado contra lo que trae ese token, así que una
/// ruta nueva que lo admita rompe este test en vez de abrirse callada.
/// </summary>
public sealed class DoorStaffAccessTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public DoorStaffAccessTests(EventsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MappedEndpoints_ReturnsOnlyTheDoorEventList_WhenTheCallerIsDoorStaff()
    {
        var authorized = await EndpointAccess.AuthorizedEndpointsAsync(
            _factory.Services, TestAuthHandler.DoorStaffRoles.Split(','));

        authorized.ShouldBe(["ListDoorEvents"]);
    }
}

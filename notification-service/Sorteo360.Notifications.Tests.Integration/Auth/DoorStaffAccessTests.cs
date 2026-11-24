using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Auth;

/// <summary>
/// Quien está en la puerta no llega a nada de este servicio (spec <c>09</c> §8.1, ADR-0025): su
/// token no lleva <c>platform:staff</c>, así que la fallback policy lo deja fuera, y ninguna política
/// propia pide un rol de la puerta. Se recorre cada endpoint mapeado contra lo que trae ese token,
/// así que una ruta nueva que lo admita rompe este test en vez de abrirse callada.
/// </summary>
public sealed class DoorStaffAccessTests : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory _factory;

    public DoorStaffAccessTests(NotificationsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MappedEndpoints_ReturnsNone_WhenTheCallerIsDoorStaff()
    {
        var authorized = await EndpointAccess.AuthorizedEndpointsAsync(
            _factory.Services, TestAuthHandler.DoorStaffRoles.Split(','));

        authorized.ShouldBeEmpty();
    }
}

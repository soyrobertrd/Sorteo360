using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Auth;

/// <summary>
/// Quien está en la puerta llega a la puerta y a nada más (spec <c>09</c> §8.1, ADR-0025). Se
/// recorre cada endpoint mapeado contra lo que trae su token, así que una ruta nueva que lo
/// admita rompe este test en vez de abrirse callada.
/// </summary>
public sealed class DoorStaffAccessTests : IClassFixture<ConvocatoriasApiFactory>
{
    /// <summary>Las cuatro rutas de la puerta: escaneo, check-in elegido, deshecha y lista de acceso.</summary>
    private static readonly string[] DoorEndpoints =
    [
        "CreateCheckIn",
        "CreateCheckInByRegistration",
        "ListDoorRegistrations",
        "UndoCheckIn",
    ];

    private readonly ConvocatoriasApiFactory _factory;

    public DoorStaffAccessTests(ConvocatoriasApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MappedEndpoints_ReturnsOnlyTheDoorRoutes_WhenTheCallerIsDoorStaff()
    {
        var authorized = await EndpointAccess.AuthorizedEndpointsAsync(
            _factory.Services, TestAuthHandler.DoorStaffRoles.Split(','));

        authorized.Order(StringComparer.Ordinal).ShouldBe(DoorEndpoints);
    }
}

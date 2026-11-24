using System.Net;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Una entrega con el almacenamiento caído (spec 03 §3.2): es una caída y no un rechazo, así que es
/// <c>503</c> y no se escribe nada. Clase y fábrica propias, porque tira abajo el almacenamiento.
/// </summary>
public sealed class PrizeHandoverFileStoreOutageTests : IClassFixture<RaffleFileStoreOutageApiDbFactory>
{
    private readonly RaffleFileStoreOutageApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public PrizeHandoverFileStoreOutageTests(RaffleFileStoreOutageApiDbFactory factory)
    {
        _factory = factory;
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    [Theory]
    [InlineData(FileStoreFault.Unavailable)]
    [InlineData(FileStoreFault.Timeout)]
    public async Task HandOver_Returns503AndWritesNothing_WhenTheFileStoreIsDown(FileStoreFault fault)
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var rows = await _api.CountStoredFileRowsAsync();

        HttpResponseMessage response;
        using (_factory.Failing(fault))
            response = await _api.HandOverAsync(eventId, prizeId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "FILE_STORE_UNAVAILABLE");
        (await _api.GetStoredHandoversAsync(eventId)).ShouldBeEmpty();
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>GET /v1/prize-draws/by-code/{code}</c> (spec 03 §2.4): encuentra el sorteo desde lo que alguien
/// escribió en un mostrador, responde los códigos muertos en vez de esconderlos y no escribe nada.
/// </summary>
public sealed class PrizeDrawLookupEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public PrizeDrawLookupEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region La normalización

    [Fact]
    public async Task FindByCode_Returns200_WhenTheCodeIsWrittenAnyWayItReads()
    {
        var (eventId, prizeId) = await DrawnPrizeWithCodeAsync("RD-7K3M-Q9XA");

        foreach (var written in new[] { "RD-7K3M-Q9XA", "rd7k3mq9xa", "RD 7K3M Q9XA", "7K3MQ9XA", "7k3m-q9xa" })
        {
            var response = await FindAsync(written);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{written}: {await response.Content.ReadAsStringAsync()}");
            var found = (await response.Content.ReadFromJsonAsync<PrizeDrawLookupResponse>())!;
            found.EventId.ShouldBe(eventId);
            found.PrizeId.ShouldBe(prizeId);
        }
    }

    [Fact]
    public async Task FindByCode_Returns200_WhenTheCodeIsWrittenWithTheLettersItCannotContain()
    {
        var (_, prizeId) = await DrawnPrizeWithCodeAsync("RD-1K30-Q9XA");

        var response = await FindAsync("rd-lk3o-q9xa");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<PrizeDrawLookupResponse>())!.PrizeId.ShouldBe(prizeId);
    }

    [Theory]
    [InlineData("RD-ZZZZ-ZZZZ")]
    [InlineData("RD-7K3M")]
    [InlineData("RD-7K3M-Q9XU")]
    [InlineData("María Pérez")]
    [InlineData("RD-7K3M-Q9X%")]
    public async Task FindByCode_Returns404RedemptionCodeNotFound_WhenNoDrawHasTheCode(string written)
    {
        await RaffleTestApi.ShouldBeProblemAsync(
            await FindAsync(written), HttpStatusCode.NotFound, "REDEMPTION_CODE_NOT_FOUND");
    }

    #endregion

    #region Lo que responde

    [Fact]
    public async Task FindByCode_Returns200Drawn_WhenTheDrawIsLiveWithNoHandover()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var draw = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();
        var raffle = await _api.GetRaffleAsync(eventId);
        var prize = raffle.Rounds.SelectMany(r => r.Prizes).Single(p => p.Id == prizeId);

        var found = await FindOkAsync(draw.RedemptionCode);

        found.DrawId.ShouldBe(draw.Id);
        found.EventId.ShouldBe(eventId);
        found.RaffleId.ShouldBe(raffle.Id);
        found.PrizeId.ShouldBe(prizeId);
        found.PrizeName.ShouldBe(prize.Name);
        found.WinnerName.ShouldBe(prize.Draws[0].WinnerName);
        found.WinnerCompany.ShouldBe(prize.Draws[0].WinnerCompany);
        found.Status.ShouldBe(PrizeDrawStatus.Drawn);
        found.Void.ShouldBeNull();
        found.Handover.ShouldBeNull();
    }

    [Fact]
    public async Task FindByCode_Returns200RedeemedWithTheHandover_WhenThePrizeWasHandedOver()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId, RaffleTestApi.HandoverForm(receiverName: "Ana Gómez")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var draw = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();
        var handover = (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem();

        var found = await FindOkAsync(draw.RedemptionCode);

        found.Status.ShouldBe(PrizeDrawStatus.Redeemed);
        found.Void.ShouldBeNull();
        found.Handover.ShouldNotBeNull();
        found.Handover.ReceiverName.ShouldBe("Ana Gómez");
        found.Handover.RedeemedAt.ShouldBe(handover.RedeemedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task FindByCode_Returns200VoidedWithTheVoid_WhenTheDrawWasVoided()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.VoidAsync(eventId, prizeId, PrizeDrawVoidCode.Declined)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var draw = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();

        var found = await FindOkAsync(draw.RedemptionCode);

        found.Status.ShouldBe(PrizeDrawStatus.Voided);
        found.Handover.ShouldBeNull();
        found.Void.ShouldNotBeNull();
        found.Void.Code.ShouldBe(PrizeDrawVoidCode.Declined);
        found.Void.VoidedAt.ShouldBe(draw.VoidedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task FindByCode_Returns200DrawnWithNoHandover_WhenTheHandoverWasReversed()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.ReverseHandoverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var draw = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();

        var found = await FindOkAsync(draw.RedemptionCode);

        found.Status.ShouldBe(PrizeDrawStatus.Drawn);
        found.Handover.ShouldBeNull();
    }

    [Fact]
    public async Task FindByCode_ReturnsWithoutChangingAnyRow_WhenLookingUpEveryKindOfCode()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 4, prizes: 3);
        foreach (var prizeId in prizes)
            (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.HandOverAsync(eventId, prizes[1])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[2], PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var draws = await _api.GetStoredDrawsAsync(eventId);
        var handovers = await _api.GetStoredHandoversAsync(eventId);
        var raffle = await _api.GetStoredRaffleAsync(eventId);

        foreach (var draw in draws)
            await FindOkAsync(draw.RedemptionCode.ToLowerInvariant());
        await FindAsync("RD-ZZZZ-ZZZZ");

        (await _api.GetStoredDrawsAsync(eventId)).ShouldBe(draws);
        (await _api.GetStoredHandoversAsync(eventId)).ShouldBe(handovers);
        (await _api.GetStoredRaffleAsync(eventId)).ShouldBe(raffle);
    }

    #endregion

    #region Lo que no existe

    /// <summary>
    /// Sin búsqueda por nombre, sin una ruta que entregue desde un código y sin una descarga de
    /// archivos propia del sorteo: la firma se lee por su id en <c>/files</c> como cualquier archivo
    /// (spec 03 §2.4, Definition of done).
    /// </summary>
    [Fact]
    public void Endpoints_ReturnNoNameSearchNoRedeemFromCodeAndNoRaffleFileDownload_WhenTheAppIsBuilt()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                Pattern = e.RoutePattern.RawText ?? string.Empty,
                Parameters = e.RoutePattern.Parameters.Select(p => p.Name).ToList(),
                Methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();

        // La lista de ganadores también cuelga de prize-draws; la búsqueda es la única que toma un código.
        var lookups = endpoints
            .Where(e => e.Pattern.Contains("prize-draws", StringComparison.Ordinal) && e.Parameters.Contains("code"))
            .ToList();
        var lookup = lookups.ShouldHaveSingleItem();
        lookup.Pattern.ShouldEndWith("prize-draws/by-code/{code}");
        lookup.Methods.ShouldBe(["GET"]);
        lookup.Parameters.ShouldBe(["version", "code"], ignoreOrder: true);

        endpoints.ShouldNotContain(e =>
            e.Parameters.Contains("code") && !e.Methods.SequenceEqual(new[] { "GET" }));
        // El documento de OpenAPI lleva {documentName}; lo que importa son las rutas del sorteo.
        endpoints
            .Where(e => e.Pattern.Contains("raffle", StringComparison.Ordinal) || e.Pattern.Contains("prize-draws", StringComparison.Ordinal))
            .ShouldNotContain(e => e.Parameters.Any(p => p.Contains("name", StringComparison.OrdinalIgnoreCase)));
        endpoints.ShouldNotContain(e =>
            e.Pattern.Contains("raffle", StringComparison.Ordinal)
            && (e.Pattern.Contains("file", StringComparison.OrdinalIgnoreCase)
                || e.Pattern.Contains("signature", StringComparison.OrdinalIgnoreCase)
                || e.Pattern.Contains("content", StringComparison.OrdinalIgnoreCase)));
    }

    #endregion

    private Task<HttpResponseMessage> FindAsync(string written) =>
        _client.GetAsync($"v1/prize-draws/by-code/{Uri.EscapeDataString(written)}");

    private async Task<PrizeDrawLookupResponse> FindOkAsync(string written)
    {
        var response = await FindAsync(written);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{written}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<PrizeDrawLookupResponse>())!;
    }

    /// <summary>
    /// Un premio sorteado cuyo código se fija a mano, para probar lo que escribiría alguien con un
    /// código conocido. Cada test usa uno distinto: el índice es único en toda la base.
    /// </summary>
    private async Task<(Guid EventId, Guid PrizeId)> DrawnPrizeWithCodeAsync(string code)
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE events.prize_draw SET redemption_code = {code} WHERE prize_id = {prizeId}");

        return (eventId, prizeId);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Lo que del armado deja de poder tocarse con el sorteo en curso: un premio o una ronda con sorteos
/// (spec 00 §4, <c>PRIZE_HAS_DRAWS</c>). Las dos reglas, en cambio, siguen editables y rigen desde el
/// próximo sorteo (spec 02 §1.2). Además, lo que no existe a propósito: ni redraw, ni reset, ni
/// Idempotency-Key.
/// </summary>
public sealed class RaffleDrawnSetupEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleDrawnSetupEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region PRIZE_HAS_DRAWS

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdatePrize_Returns409PrizeHasDrawsAndChangesNothing_WhenThePrizeHasADraw(bool voided)
    {
        var (eventId, prizeId) = await DrawnPrizeAsync(voided);
        var roundId = await RoundOfAsync(eventId, prizeId);

        var response = await _client.PatchAsJsonAsync(
            RaffleTestApi.PrizeUrl(eventId, prizeId), new UpdatePrizeRequest { RoundId = roundId, Name = "Otro" });

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "PRIZE_HAS_DRAWS");
        (await PrizeAsync(eventId, prizeId)).Name.ShouldBe("Bolsa");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletePrize_Returns409PrizeHasDrawsAndKeepsIt_WhenThePrizeHasADraw(bool voided)
    {
        var (eventId, prizeId) = await DrawnPrizeAsync(voided);

        var response = await _client.DeleteAsync(RaffleTestApi.PrizeUrl(eventId, prizeId));

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "PRIZE_HAS_DRAWS");
        (await PrizeAsync(eventId, prizeId)).Draws.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UpdateAndDeletePrize_Return204_WhenOnlyAnotherPrizeIsDrawn()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 2);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        var roundId = await RoundOfAsync(eventId, prizes[1]);

        var patch = await _client.PatchAsJsonAsync(
            RaffleTestApi.PrizeUrl(eventId, prizes[1]), new UpdatePrizeRequest { RoundId = roundId, Name = "Otro" });
        var delete = await _client.DeleteAsync(RaffleTestApi.PrizeUrl(eventId, prizes[1]));

        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent, await patch.Content.ReadAsStringAsync());
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, await delete.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdatePrize_RunsFirstOrReturnsPrizeHasDraws_WhenItRacesADrawOnThatPrize()
    {
        for (var run = 0; run < 10; run++)
        {
            var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
            var roundId = await RoundOfAsync(eventId, prizes[0]);

            var responses = await Task.WhenAll(
                _client.PatchAsJsonAsync(
                    RaffleTestApi.PrizeUrl(eventId, prizes[0]), new UpdatePrizeRequest { RoundId = roundId, Name = "Otro" }),
                _api.DrawAsync(eventId, prizes[0]));
            var (patch, draw) = (responses[0], responses[1]);

            draw.StatusCode.ShouldBe(HttpStatusCode.OK, $"vuelta {run}");
            if (patch.StatusCode != HttpStatusCode.NoContent)
                await RaffleTestApi.ShouldBeProblemAsync(patch, HttpStatusCode.Conflict, "PRIZE_HAS_DRAWS");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateRound_Returns409PrizeHasDrawsAboutTheRound_WhenOneOfItsPrizesHasADraw(bool voided)
    {
        var (eventId, prizeId) = await DrawnPrizeAsync(voided);
        var roundId = await RoundOfAsync(eventId, prizeId);

        var response = await _client.PatchAsJsonAsync(
            $"{RaffleTestApi.RoundsUrl(eventId)}/{roundId}", new UpdateRaffleRoundRequest { Name = "Apertura" });

        var body = await response.Content.ReadAsStringAsync();
        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "PRIZE_HAS_DRAWS");
        body.ShouldContain("ronda");
        (await _api.GetRaffleAsync(eventId)).Rounds.Single(r => r.Id == roundId).Name.ShouldBe("Ronda");
    }

    [Fact]
    public async Task UpdateRound_Returns204_WhenItsPrizesAreUndrawn()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        var otherRound = await _api.CreateRoundAsync(eventId, "Segunda");
        await _api.CreatePrizesAsync(eventId, otherRound);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.PatchAsJsonAsync(
            $"{RaffleTestApi.RoundsUrl(eventId)}/{otherRound}", new UpdateRaffleRoundRequest { Name = "Cierre" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Las reglas en curso

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task UpdateRaffle_Returns204WithTheNewRules_WhenInProgressAndAToggleChanges(
        bool onePrizePerPerson, bool onePrizePerCompany)
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 2);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.PatchAsJsonAsync(
            RaffleTestApi.RaffleUrl(eventId), Update(onePrizePerPerson, onePrizePerCompany));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var raffle = await _api.GetRaffleAsync(eventId);
        raffle.State.ShouldBe(RaffleState.InProgress);
        raffle.VisualStyle.ShouldBe(RaffleVisualStyle.SequentialNames);
        raffle.OnePrizePerPerson.ShouldBe(onePrizePerPerson);
        raffle.OnePrizePerCompany.ShouldBe(onePrizePerCompany);
    }

    [Fact]
    public async Task UpdateRaffle_Returns204ApplyingTheRest_WhenInProgressAndTheTogglesAreUnchanged()
    {
        var (eventId, _) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);

        var response = await _client.PatchAsJsonAsync(
            RaffleTestApi.RaffleUrl(eventId), Update(onePrizePerPerson: true, onePrizePerCompany: false));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await _api.GetRaffleAsync(eventId)).VisualStyle.ShouldBe(RaffleVisualStyle.SequentialNames);
    }

    #endregion

    #region Lo que no existe

    [Fact]
    public void Routes_HaveNoRedrawOrReset_WhenTheAppIsBuilt()
    {
        // Solo las rutas de la rifa: el reinicio de evaluaciones de un proveedor es otra cosa.
        var patterns = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Where(p => p.Contains("/raffle", StringComparison.Ordinal))
            .ToList();

        patterns.ShouldContain(p => p.EndsWith("/raffle/reopen", StringComparison.Ordinal));
        patterns.ShouldNotContain(p => p.Contains("redraw", StringComparison.OrdinalIgnoreCase));
        patterns.ShouldNotContain(p => p.Contains("reset", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Un reintento con la misma clave no recupera nada: el servidor no la lee (spec 02 §7.1).</summary>
    [Fact]
    public async Task Draw_IgnoresTheIdempotencyKey_WhenARetryCarriesTheSameOne()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);

        HttpRequestMessage Request()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, RaffleTestApi.DrawUrl(eventId, prizes[0]));
            request.Headers.Add("Idempotency-Key", "misma-clave");
            return request;
        }

        (await _client.SendAsync(Request())).StatusCode.ShouldBe(HttpStatusCode.OK);
        await RaffleTestApi.ShouldBeProblemAsync(await _client.SendAsync(Request()), HttpStatusCode.Conflict, "PRIZE_ALREADY_DRAWN");
    }

    #endregion

    /// <summary>Un premio sorteado, y con su único sorteo anulado si se pide.</summary>
    private async Task<(Guid EventId, Guid PrizeId)> DrawnPrizeAsync(bool voided)
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        if (voided)
            (await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Declined)).StatusCode.ShouldBe(HttpStatusCode.OK);

        return (eventId, prizes[0]);
    }

    private async Task<PrizeResponse> PrizeAsync(Guid eventId, Guid prizeId) =>
        (await _api.GetRaffleAsync(eventId)).Rounds.SelectMany(r => r.Prizes).Single(p => p.Id == prizeId);

    private async Task<Guid> RoundOfAsync(Guid eventId, Guid prizeId) => (await PrizeAsync(eventId, prizeId)).RoundId;

    /// <summary>Todo distinto de los valores por omisión salvo, si se pide, las dos reglas.</summary>
    private static UpdateRaffleRequest Update(bool onePrizePerPerson, bool onePrizePerCompany) => new()
    {
        VisualStyle = RaffleVisualStyle.SequentialNames,
        ShowPrizeOnScreen = false,
        AnimationSeconds = 12,
        RequiresPresenceConfirmation = true,
        OnePrizePerPerson = onePrizePerPerson,
        OnePrizePerCompany = onePrizePerCompany,
    };
}

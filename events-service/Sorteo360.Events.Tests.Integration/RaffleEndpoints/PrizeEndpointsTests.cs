using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

public sealed class PrizeEndpointsTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public PrizeEndpointsTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    private static string PrizeUrl(Guid eventId, Guid prizeId) => $"{RaffleTestApi.PrizesUrl(eventId)}/{prizeId}";

    [Fact]
    public async Task CreatePrizes_Returns201WithFiveIdenticalPrizes_WhenQuantityIsFive()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);

        var create = await _client.PostAsJsonAsync(RaffleTestApi.PrizesUrl(eventId), new CreatePrizesRequest
        {
            RoundId = roundId,
            Name = "Bolsa de tela",
            Description = "Con el logo del evento",
            Value = "RD$500",
            Quantity = 5,
        });

        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        create.Headers.Location!.ToString().ShouldBe($"/v1/events/{eventId}/raffle");
        var created = (await create.Content.ReadFromJsonAsync<List<PrizeResponse>>())!;
        created.Count.ShouldBe(5);
        created.Select(p => p.Id).Distinct().Count().ShouldBe(5);
        created.ShouldAllBe(p => p.Name == "Bolsa de tela" && p.RoundId == roundId && p.Value == "RD$500"
            && p.Description == "Con el logo del evento");

        var prizes = (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes;
        prizes.Select(p => p.Id).ShouldBe(created.Select(p => p.Id), ignoreOrder: true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(null)]
    public async Task CreatePrizes_Returns400_WhenQuantityIsOutOfRangeOrMissing(int? quantity)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);

        var create = await _client.PostAsJsonAsync(
            RaffleTestApi.PrizesUrl(eventId), new CreatePrizesRequest { RoundId = roundId, Name = "Bolsa", Quantity = quantity });

        create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePrizes_Returns201_WhenQuantityIsFifty()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);

        var created = await _api.CreatePrizesAsync(eventId, roundId, quantity: 50);

        created.Count.ShouldBe(50);
    }

    [Fact]
    public async Task CreatePrizes_Returns404RoundNotFound_WhenTheRoundIsOfAnotherRaffle()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var foreignRound = await _api.CreateRoundAsync(await _api.CreateEventWithRaffleAsync());

        var create = await _client.PostAsJsonAsync(
            RaffleTestApi.PrizesUrl(eventId), new CreatePrizesRequest { RoundId = foreignRound, Name = "Bolsa", Quantity = 1 });

        await RaffleTestApi.ShouldBeProblemAsync(create, HttpStatusCode.NotFound, "ROUND_NOT_FOUND");
    }

    [Fact]
    public async Task UpdatePrize_Returns204ClearingOmittedFields_WhenOnlyRequiredFieldsAreSent()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var create = await _client.PostAsJsonAsync(RaffleTestApi.PrizesUrl(eventId), new CreatePrizesRequest
        {
            RoundId = roundId, Name = "Ron", Description = "Añejo", Value = "RD$2,000", Quantity = 1,
        });
        var prize = (await create.Content.ReadFromJsonAsync<List<PrizeResponse>>())!.Single();

        var patch = await _client.PatchAsJsonAsync(
            PrizeUrl(eventId, prize.Id), new UpdatePrizeRequest { RoundId = roundId, Name = "3 botellas de ron" });

        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single();
        stored.Name.ShouldBe("3 botellas de ron");
        stored.Description.ShouldBeNull();
        stored.Value.ShouldBeNull();
        stored.CreatedAt.ShouldBe(prize.CreatedAt);
    }

    [Theory]
    [InlineData(nameof(UpdatePrizeRequest.Name))]
    [InlineData(nameof(UpdatePrizeRequest.RoundId))]
    public async Task UpdatePrize_Returns400_WhenARequiredFieldIsOmitted(string field)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var prize = (await _api.CreatePrizesAsync(eventId, roundId)).Single();
        var request = field == nameof(UpdatePrizeRequest.Name)
            ? new UpdatePrizeRequest { RoundId = roundId }
            : new UpdatePrizeRequest { Name = "Bolsa" };

        var patch = await _client.PatchAsJsonAsync(PrizeUrl(eventId, prize.Id), request);

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePrize_Returns204MovingThePrize_WhenTheRoundIdIsAnotherRoundOfTheRaffle()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var first = await _api.CreateRoundAsync(eventId, "Uno");
        var second = await _api.CreateRoundAsync(eventId, "Dos");
        var prize = (await _api.CreatePrizesAsync(eventId, first)).Single();

        var patch = await _client.PatchAsJsonAsync(
            PrizeUrl(eventId, prize.Id), new UpdatePrizeRequest { RoundId = second, Name = prize.Name });

        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rounds = (await _api.GetRaffleAsync(eventId)).Rounds;
        rounds[0].Prizes.ShouldBeEmpty();
        rounds[1].Prizes.Single().Id.ShouldBe(prize.Id);
        rounds[1].Prizes.Single().RoundId.ShouldBe(second);
    }

    [Fact]
    public async Task UpdatePrize_Returns404RoundNotFound_WhenTheRoundIsOfAnotherEventsRaffle()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var prize = (await _api.CreatePrizesAsync(eventId, roundId)).Single();
        var foreignRound = await _api.CreateRoundAsync(await _api.CreateEventWithRaffleAsync());

        var patch = await _client.PatchAsJsonAsync(
            PrizeUrl(eventId, prize.Id), new UpdatePrizeRequest { RoundId = foreignRound, Name = "Bolsa" });

        await RaffleTestApi.ShouldBeProblemAsync(patch, HttpStatusCode.NotFound, "ROUND_NOT_FOUND");
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Id.ShouldBe(prize.Id);
    }

    [Fact]
    public async Task UpdatePrize_Returns404PrizeNotFound_WhenThePrizeIsOfAnotherRaffle()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var otherEventId = await _api.CreateEventWithRaffleAsync();
        var foreignPrize = (await _api.CreatePrizesAsync(otherEventId, await _api.CreateRoundAsync(otherEventId))).Single();

        var patch = await _client.PatchAsJsonAsync(
            PrizeUrl(eventId, foreignPrize.Id), new UpdatePrizeRequest { RoundId = roundId, Name = "Bolsa" });

        await RaffleTestApi.ShouldBeProblemAsync(patch, HttpStatusCode.NotFound, "PRIZE_NOT_FOUND");
    }

    [Fact]
    public async Task DeletePrize_Returns204AndRemovesOnlyThatPrize_WhenItExists()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var prizes = await _api.CreatePrizesAsync(eventId, roundId, quantity: 2);

        var delete = await _client.DeleteAsync(PrizeUrl(eventId, prizes[0].Id));

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Id.ShouldBe(prizes[1].Id);
    }

    [Fact]
    public async Task DeletePrize_Returns404PrizeNotFound_WhenThePrizeDoesNotExist()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var delete = await _client.DeleteAsync(PrizeUrl(eventId, Guid.NewGuid()));

        await RaffleTestApi.ShouldBeProblemAsync(delete, HttpStatusCode.NotFound, "PRIZE_NOT_FOUND");
    }

    [Fact]
    public async Task GetRaffle_ReturnsRoundsByNumberAndPrizesByCreation_WhenSeveralExist()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var first = await _api.CreateRoundAsync(eventId, "Uno");
        var second = await _api.CreateRoundAsync(eventId, "Dos");
        var early = (await _api.CreatePrizesAsync(eventId, second, "Primero")).Single();
        var late = (await _api.CreatePrizesAsync(eventId, second, "Segundo")).Single();
        await _api.CreatePrizesAsync(eventId, first, "Del uno");

        var rounds = (await _api.GetRaffleAsync(eventId)).Rounds;

        rounds.Select(r => r.Id).ShouldBe([first, second]);
        rounds[1].Prizes.Select(p => p.Id).ShouldBe([early.Id, late.Id]);
    }
}

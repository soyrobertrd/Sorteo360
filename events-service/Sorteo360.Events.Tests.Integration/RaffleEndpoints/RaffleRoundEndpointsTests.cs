using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

public sealed class RaffleRoundEndpointsTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleRoundEndpointsTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    private static string RoundUrl(Guid eventId, Guid roundId) => $"{RaffleTestApi.RoundsUrl(eventId)}/{roundId}";

    [Fact]
    public async Task CreateRound_Returns201NumberingInCreationOrder_WhenSeveralAreCreated()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var create = await _client.PostAsJsonAsync(
            RaffleTestApi.RoundsUrl(eventId), new CreateRaffleRoundRequest { Name = "Apertura", Description = "Primeros premios" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        create.Headers.Location!.ToString().ShouldBe($"/v1/events/{eventId}/raffle");
        var first = await create.Content.ReadFromJsonAsync<Guid>();
        await _api.CreateRoundAsync(eventId, "Intermedia");
        await _api.CreateRoundAsync(eventId, "Final");

        var raffle = await _api.GetRaffleAsync(eventId);
        raffle.Rounds.Select(r => (r.Number, r.Name)).ShouldBe([(1, "Apertura"), (2, "Intermedia"), (3, "Final")]);
        raffle.Rounds[0].Id.ShouldBe(first);
        raffle.Rounds[0].Description.ShouldBe("Primeros premios");
        raffle.Rounds[0].Prizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteRound_Returns204RenumberingTheRoundsBehind_WhenTheRoundIsEmpty()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var first = await _api.CreateRoundAsync(eventId, "Uno");
        var second = await _api.CreateRoundAsync(eventId, "Dos");
        var third = await _api.CreateRoundAsync(eventId, "Tres");

        var delete = await _client.DeleteAsync(RoundUrl(eventId, second));

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var raffle = await _api.GetRaffleAsync(eventId);
        raffle.Rounds.Select(r => (r.Id, r.Number)).ShouldBe([(first, 1), (third, 2)]);
    }

    [Fact]
    public async Task DeleteRound_Returns409RoundNotEmpty_WhenTheRoundHoldsAPrize()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var first = await _api.CreateRoundAsync(eventId, "Uno");
        await _api.CreateRoundAsync(eventId, "Dos");
        await _api.CreatePrizesAsync(eventId, first);
        var before = await _api.GetRaffleAsync(eventId);

        var delete = await _client.DeleteAsync(RoundUrl(eventId, first));

        await RaffleTestApi.ShouldBeProblemAsync(delete, HttpStatusCode.Conflict, "ROUND_NOT_EMPTY");
        var after = await _api.GetRaffleAsync(eventId);
        after.Rounds.Select(r => (r.Id, r.Number, r.Prizes.Count))
            .ShouldBe(before.Rounds.Select(r => (r.Id, r.Number, r.Prizes.Count)));
    }

    [Fact]
    public async Task UpdateRound_Returns204ReplacingNameAndClearingDescription_WhenDescriptionIsOmitted()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var create = await _client.PostAsJsonAsync(
            RaffleTestApi.RoundsUrl(eventId), new CreateRaffleRoundRequest { Name = "Uno", Description = "Algo" });
        var roundId = await create.Content.ReadFromJsonAsync<Guid>();

        var patch = await _client.PatchAsJsonAsync(RoundUrl(eventId, roundId), new UpdateRaffleRoundRequest { Name = "Apertura" });

        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var round = (await _api.GetRaffleAsync(eventId)).Rounds.Single();
        round.Name.ShouldBe("Apertura");
        round.Description.ShouldBeNull();
        round.Number.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpdateRound_Returns400_WhenTheNameIsMissing(string? name)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);

        var patch = await _client.PatchAsJsonAsync(RoundUrl(eventId, roundId), new UpdateRaffleRoundRequest { Name = name });

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRound_Returns400_WhenTheNameIsTooLong()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var create = await _client.PostAsJsonAsync(
            RaffleTestApi.RoundsUrl(eventId), new CreateRaffleRoundRequest { Name = new string('a', 201) });

        create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRound_Returns404RoundNotFound_WhenTheRoundIsOfAnotherRaffle()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var otherEventId = await _api.CreateEventWithRaffleAsync();
        var foreignRound = await _api.CreateRoundAsync(otherEventId);

        var patch = await _client.PatchAsJsonAsync(RoundUrl(eventId, foreignRound), new UpdateRaffleRoundRequest { Name = "X" });

        await RaffleTestApi.ShouldBeProblemAsync(patch, HttpStatusCode.NotFound, "ROUND_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteRound_Returns404RoundNotFound_WhenTheRoundDoesNotExist()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var delete = await _client.DeleteAsync(RoundUrl(eventId, Guid.NewGuid()));

        await RaffleTestApi.ShouldBeProblemAsync(delete, HttpStatusCode.NotFound, "ROUND_NOT_FOUND");
    }

    [Fact]
    public async Task CreateRound_Returns404RaffleNotFound_WhenTheEventHasNoRaffle()
    {
        var eventId = await _api.CreateEventAsync();

        var create = await _client.PostAsJsonAsync(RaffleTestApi.RoundsUrl(eventId), new CreateRaffleRoundRequest { Name = "Uno" });

        await RaffleTestApi.ShouldBeProblemAsync(create, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }
}

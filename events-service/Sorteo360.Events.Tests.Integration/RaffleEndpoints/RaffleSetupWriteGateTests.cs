using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Lo que vale igual para las seis escrituras de rondas y premios: el rol que exigen y el estado del
/// evento que las frena. Cada caso arma un sorteo con una ronda y un premio, y manda la escritura
/// con un cuerpo válido, para que lo único que pueda rechazarla sea lo que se prueba.
/// </summary>
public sealed class RaffleSetupWriteGateTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleSetupWriteGateTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    public static TheoryData<string> Writes =>
    [
        "CreateRound", "UpdateRound", "DeleteRound", "CreatePrizes", "UpdatePrize", "DeletePrize",
    ];

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task SetupWrite_Returns409EventNotEditable_WhenTheEventIsClosed(string write) =>
        await AssertEventNotEditableAsync(write, "closed");

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task SetupWrite_Returns409EventNotEditable_WhenTheEventIsCancelled(string write) =>
        await AssertEventNotEditableAsync(write, "cancelled");

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task SetupWrite_Returns403_WhenTheCallerLacksRafflesWrite(string write)
    {
        var target = await ArrangeAsync();

        var response = await SendAsync(write, target, AuthorizationPolicies.RafflesDraw);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task AssertEventNotEditableAsync(string write, string status)
    {
        var target = await ArrangeAsync();
        await _api.SetEventStatusAsync(target.EventId, status);

        var response = await SendAsync(write, target, roles: null);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
        (await _api.GetRaffleAsync(target.EventId)).Rounds.Single().Prizes.Single().Id.ShouldBe(target.PrizeId);
    }

    private async Task<Target> ArrangeAsync()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var prize = (await _api.CreatePrizesAsync(eventId, roundId)).Single();
        return new Target(eventId, roundId, prize.Id);
    }

    /// <summary>Sin <paramref name="roles"/>, con los del usuario de prueba por omisión.</summary>
    private Task<HttpResponseMessage> SendAsync(string write, Target target, string? roles)
    {
        var rounds = RaffleTestApi.RoundsUrl(target.EventId);
        var prizes = RaffleTestApi.PrizesUrl(target.EventId);

        var (method, url, content) = write switch
        {
            "CreateRound" => (HttpMethod.Post, rounds, JsonContent.Create(new CreateRaffleRoundRequest { Name = "Otra" })),
            "UpdateRound" => (HttpMethod.Patch, $"{rounds}/{target.RoundId}", JsonContent.Create(new UpdateRaffleRoundRequest { Name = "Otra" })),
            "DeleteRound" => (HttpMethod.Delete, $"{rounds}/{target.RoundId}", null),
            "CreatePrizes" => (HttpMethod.Post, prizes, JsonContent.Create(new CreatePrizesRequest { RoundId = target.RoundId, Name = "Otro", Quantity = 1 })),
            "UpdatePrize" => (HttpMethod.Patch, $"{prizes}/{target.PrizeId}", JsonContent.Create(new UpdatePrizeRequest { RoundId = target.RoundId, Name = "Otro" })),
            "DeletePrize" => (HttpMethod.Delete, $"{prizes}/{target.PrizeId}", (HttpContent?)null),
            _ => throw new ArgumentOutOfRangeException(nameof(write), write, null),
        };

        return roles is null
            ? _client.SendAsync(new HttpRequestMessage(method, url) { Content = content })
            : _api.SendAsync(method, url, content, roles);
    }

    private sealed record Target(Guid EventId, Guid RoundId, Guid PrizeId);
}

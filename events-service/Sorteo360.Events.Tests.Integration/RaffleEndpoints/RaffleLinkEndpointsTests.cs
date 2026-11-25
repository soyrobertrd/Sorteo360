using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Sorteos vinculados: varios sorteos de un mismo evento no repiten ganadores. Quien tiene un premio
/// vivo en uno queda fuera del bombo de los demás, se vea en el bombo, en el giro o en el padrón.
/// </summary>
public sealed class RaffleLinkEndpointsTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleLinkEndpointsTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task Draw_LeavesOutTheWinnerOfALinkedRaffle()
    {
        var (first, firstPrize) = await RaffleWithTwoPeopleAsync();
        var (second, secondPrize) = await RaffleWithTwoPeopleAsync();
        await LinkAsync(second, first);

        (await _api.StartAsync(first)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(first, firstPrize)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var winner = (await _api.GetStoredDrawsAsync(first)).ShouldHaveSingleItem().WinnerIdentityKey;
        var other = winner == "ana@banreservas.example" ? "luis@banreservas.example" : "ana@banreservas.example";

        (await ReasonsAsync(second))[winner].ShouldBe("won_in_linked_raffle");
        (await _api.StartAsync(second)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var eligible = await _client.GetFromJsonAsync<EligibleResponse>(
            $"{RaffleTestApi.PrizeUrl(second, secondPrize)}/eligible");
        eligible!.Count.ShouldBe(1);

        var draw = await _api.DrawAsync(second, secondPrize);

        draw.StatusCode.ShouldBe(HttpStatusCode.OK, await draw.Content.ReadAsStringAsync());
        (await _api.GetStoredDrawsAsync(second)).ShouldHaveSingleItem().WinnerIdentityKey.ShouldBe(other);
    }

    [Fact]
    public async Task Draw_IsAlsoBlockedTheOtherWay_ForTheRaffleThatWasLinkedTo()
    {
        var (first, firstPrize) = await RaffleWithTwoPeopleAsync();
        var (second, secondPrize) = await RaffleWithTwoPeopleAsync();
        await LinkAsync(second, first);

        (await _api.StartAsync(second)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(second, secondPrize)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var winner = (await _api.GetStoredDrawsAsync(second)).ShouldHaveSingleItem().WinnerIdentityKey;

        (await _api.StartAsync(first)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(first, firstPrize)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _api.GetStoredDrawsAsync(first)).ShouldHaveSingleItem().WinnerIdentityKey.ShouldNotBe(winner);
    }

    /// <summary>La misma persona con correo en un padrón y solo con su ID en el otro.</summary>
    [Fact]
    public async Task Draw_RecognisesTheLinkedWinnerByEmployeeId_WhenThePadronsKeyHerDifferently()
    {
        var first = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(first, new RaffleParticipantInputDto(FullName: "Ana", Email: "ana@banreservas.example", EmployeeCode: "E-1", CheckedIn: true));
        var firstPrize = (await _api.CreatePrizesAsync(first, await _api.CreateRoundAsync(first)))[0].Id;
        var second = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(second,
            new RaffleParticipantInputDto(FullName: "Ana", EmployeeCode: " e-1 ", CheckedIn: true),
            new RaffleParticipantInputDto(FullName: "Luis", EmployeeCode: "E-2", CheckedIn: true));
        var secondPrize = (await _api.CreatePrizesAsync(second, await _api.CreateRoundAsync(second)))[0].Id;
        await LinkAsync(second, first);
        (await _api.StartAsync(first)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(first, firstPrize)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.StartAsync(second)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _api.DrawAsync(second, secondPrize)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _api.GetStoredDrawsAsync(second)).ShouldHaveSingleItem().WinnerIdentityKey.ShouldBe("id:e-2");
    }

    [Fact]
    public async Task Patch_StoresTheLink_AndLinkingToALinkedRaffleGoesToItsEvent()
    {
        var anchor = await _api.CreateEventWithRaffleAsync();
        var second = await _api.CreateEventWithRaffleAsync();
        var third = await _api.CreateEventWithRaffleAsync();
        await LinkAsync(second, anchor);

        await LinkAsync(third, second);

        (await _api.GetRaffleAsync(second)).LinkedEventId.ShouldBe(anchor);
        (await _api.GetRaffleAsync(third)).LinkedEventId.ShouldBe(anchor);
    }

    [Fact]
    public async Task Patch_Returns409_WhenLinkingToItsOwnEvent()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await PatchLinkAsync(eventId, eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_LINK_INVALID");
    }

    [Fact]
    public async Task Patch_Returns409_WhenTheRaffleIsAlreadyTheAnchorOfOthers()
    {
        var anchor = await _api.CreateEventWithRaffleAsync();
        var linked = await _api.CreateEventWithRaffleAsync();
        var elsewhere = await _api.CreateEventWithRaffleAsync();
        await LinkAsync(linked, anchor);

        var response = await PatchLinkAsync(anchor, elsewhere);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_LINK_INVALID");
    }

    [Fact]
    public async Task Patch_Returns409_WhenTheEventDoesNotExist()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await PatchLinkAsync(eventId, Guid.NewGuid());

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_LINK_INVALID");
    }

    private Task<HttpResponseMessage> PatchLinkAsync(Guid eventId, Guid linkedEventId) =>
        _client.PatchAsJsonAsync(
            RaffleTestApi.RaffleUrl(eventId),
            RaffleTestApi.ReadyToCompleteUpdate() with { LinkedEventId = linkedEventId });

    private async Task LinkAsync(Guid eventId, Guid linkedEventId)
    {
        var response = await PatchLinkAsync(eventId, linkedEventId);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private async Task ImportAsync(Guid eventId, params RaffleParticipantInputDto[] people)
    {
        var response = await _client.PostAsJsonAsync(
            $"{RaffleTestApi.RaffleUrl(eventId)}/participants/import",
            new ImportRaffleParticipantsHttpRequest(people, ReplaceExisting: true, Source: "excel"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Un sorteo con un premio y las mismas dos personas en el padrón, las dos presentes.</summary>
    private async Task<(Guid EventId, Guid PrizeId)> RaffleWithTwoPeopleAsync()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var import = await _client.PostAsJsonAsync(
            $"{RaffleTestApi.RaffleUrl(eventId)}/participants/import",
            new ImportRaffleParticipantsHttpRequest(
                [
                    new(FullName: "Ana", Email: "ana@banreservas.example", EmployeeCode: "ana", CheckedIn: true),
                    new(FullName: "Luis", Email: "luis@banreservas.example", EmployeeCode: "luis", CheckedIn: true),
                ],
                ReplaceExisting: true,
                Source: "excel"));
        import.StatusCode.ShouldBe(HttpStatusCode.OK, await import.Content.ReadAsStringAsync());
        var prize = (await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId)))[0].Id;
        return (eventId, prize);
    }

    private async Task<Dictionary<string, string?>> ReasonsAsync(Guid eventId) =>
        (await _client.GetFromJsonAsync<List<RaffleParticipantDto>>($"{RaffleTestApi.RaffleUrl(eventId)}/participants"))!
            .ToDictionary(p => p.IdentityKey, p => p.ExclusionReason, StringComparer.Ordinal);
}

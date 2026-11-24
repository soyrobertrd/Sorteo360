using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST …/prizes/{id}/draw</c> y <c>GET …/prizes/{id}/eligible</c> sobre un sorteo arrancado de
/// verdad, con la puerta en manos del test. Cada rechazo comprueba que no se escribió ningún sorteo,
/// y las dos carreras de spec 02 §4 corren contra la base.
/// </summary>
public sealed class PrizeDrawEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public PrizeDrawEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region Estado del sorteo y del evento

    [Fact]
    public async Task Draw_Returns409RaffleNotInProgress_WhenTheRaffleIsADraft()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var prizeId = (await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId)))[0].Id;

        await RaffleTestApi.ShouldBeProblemAsync(await DrawAsync(eventId, prizeId), HttpStatusCode.Conflict, "RAFFLE_NOT_IN_PROGRESS");
        await RaffleTestApi.ShouldBeProblemAsync(await EligibleAsync(eventId, prizeId), HttpStatusCode.Conflict, "RAFFLE_NOT_IN_PROGRESS");
    }

    [Fact]
    public async Task Draw_Returns409RaffleCompletedAndEligibleRaffleNotInProgress_WhenTheRaffleIsCompleted()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 1);
        await _api.SetRaffleStateAsync(eventId, RaffleState.Completed);

        await RaffleTestApi.ShouldBeProblemAsync(await DrawAsync(eventId, prizes[0]), HttpStatusCode.Conflict, "RAFFLE_COMPLETED");
        await RaffleTestApi.ShouldBeProblemAsync(await EligibleAsync(eventId, prizes[0]), HttpStatusCode.Conflict, "RAFFLE_NOT_IN_PROGRESS");
        (await CountDrawsAsync(eventId)).ShouldBe(0);
    }

    [Fact]
    public async Task Draw_Returns200_WhenTheEventIsClosed()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 1);
        await _api.SetEventStatusAsync(eventId, EventStatus.Closed);

        var response = await DrawAsync(eventId, prizes[0]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Draw_Returns404PrizeNotFound_WhenThePrizeIsAnotherRaffles()
    {
        var (eventId, _) = await StartedRaffleAsync(people: 1, prizes: 1);
        var (_, otherPrizes) = await StartedRaffleAsync(people: 1, prizes: 1);

        await RaffleTestApi.ShouldBeProblemAsync(await DrawAsync(eventId, otherPrizes[0]), HttpStatusCode.NotFound, "PRIZE_NOT_FOUND");
        await RaffleTestApi.ShouldBeProblemAsync(await EligibleAsync(eventId, otherPrizes[0]), HttpStatusCode.NotFound, "PRIZE_NOT_FOUND");
    }

    #endregion

    #region El sorteo

    [Fact]
    public async Task Draw_Returns200WithTheDrawnPrizeAndThePoolSizeEligibleCounted_WhenNothingMovesInBetween()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 3, prizes: 1);
        var eligible = await GetEligibleAsync(eventId, prizes[0]);

        var response = await DrawAsync(eventId, prizes[0]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var prize = (await response.Content.ReadFromJsonAsync<PrizeResponse>())!;
        prize.Id.ShouldBe(prizes[0]);
        prize.State.ShouldBe(PrizeState.Drawn);
        var draw = prize.Draws.ShouldHaveSingleItem();
        draw.Void.ShouldBeNull();
        draw.EligiblePoolSize.ShouldBe(eligible.Count);
        eligible.Count.ShouldBe(3);
        var winner = eligible.Participants.Single(p => p.AttendeeId == draw.WinnerAttendeeId);
        draw.WinnerName.ShouldBe(winner.DisplayName);
        draw.WinnerCompany.ShouldBe(winner.Company);
    }

    [Fact]
    public async Task Draw_Returns409PrizeAlreadyDrawnCarryingTheWinnerInTheResponsesShape_WhenThePrizeIsDrawn()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 3, prizes: 1);
        var first = (await (await DrawAsync(eventId, prizes[0])).Content.ReadFromJsonAsync<PrizeResponse>())!;

        var response = await DrawAsync(eventId, prizes[0]);

        var body = await response.Content.ReadAsStringAsync();
        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "PRIZE_ALREADY_DRAWN");
        var draw = JsonDocument.Parse(body).RootElement.GetProperty("draw");
        draw.GetProperty("id").GetGuid().ShouldBe(first.Draws[0].Id);
        draw.GetProperty("winnerName").GetString().ShouldBe(first.Draws[0].WinnerName);
        draw.TryGetProperty("winnerEmail", out _).ShouldBeFalse();
        // La forma de la respuesta del sorteo, que desde spec 03 §3.4 lleva el código (spec 02 §7.1).
        draw.GetProperty("redemptionCode").GetString().ShouldBe(first.Draws[0].RedemptionCode);
        (await CountDrawsAsync(eventId)).ShouldBe(1);
    }

    /// <summary>
    /// Apagar una regla a mitad del sorteo devuelve al bombo a quien ya ganó, desde el próximo premio,
    /// y cada sorteo dice bajo qué reglas salió (spec 02 §1.2).
    /// </summary>
    [Fact]
    public async Task Draw_NeverReturnsTheSameWinnerTwice_EvenWhenOnePrizePerPersonIsTurnedOff()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 1, prizes: 2);
        var first = (await (await DrawAsync(eventId, prizes[0])).Content.ReadFromJsonAsync<PrizeResponse>())!.Draws[0];
        var turnOff = await _client.PatchAsJsonAsync(
            RaffleTestApi.RaffleUrl(eventId), RaffleTestApi.ReadyToCompleteUpdate() with { OnePrizePerPerson = false });
        turnOff.StatusCode.ShouldBe(HttpStatusCode.NoContent, await turnOff.Content.ReadAsStringAsync());

        var eligible = await GetEligibleAsync(eventId, prizes[1]);
        var second = await DrawAsync(eventId, prizes[1]);

        // El único participante ya ganó: queda fuera del bombo y el segundo premio no se sortea.
        eligible.Participants.ShouldBeEmpty();
        await RaffleTestApi.ShouldBeProblemAsync(second, HttpStatusCode.Conflict, "RAFFLE_POOL_EXHAUSTED");
        (await _api.GetRaffleAsync(eventId)).OnePrizePerPerson.ShouldBeTrue();
        first.OnePrizePerPerson.ShouldBeTrue();
    }

    [Fact]
    public async Task Eligible_Returns409PrizeAlreadyDrawn_WhenThePrizeIsDrawn()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 1);
        (await DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        await RaffleTestApi.ShouldBeProblemAsync(await EligibleAsync(eventId, prizes[0]), HttpStatusCode.Conflict, "PRIZE_ALREADY_DRAWN");
    }

    [Fact]
    public async Task Draw_Returns409PoolExhaustedWithCountsAndWritesNothing_WhenEveryoneAlreadyWon()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 3);
        (await DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DrawAsync(eventId, prizes[1])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await DrawAsync(eventId, prizes[2]);

        var body = await response.Content.ReadAsStringAsync();
        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_POOL_EXHAUSTED");
        var root = JsonDocument.Parse(body).RootElement;
        var participants = root.GetProperty("participants").GetInt32();
        participants.ShouldBe(2);
        root.GetProperty("excludedByPerson").GetInt32().ShouldBe(2);
        (root.GetProperty("excludedByVoid").GetInt32() + root.GetProperty("excludedByPerson").GetInt32()
            + root.GetProperty("excludedByCompany").GetInt32()).ShouldBe(participants);
        (await CountDrawsAsync(eventId)).ShouldBe(2);
        (await GetEligibleAsync(eventId, prizes[2])).Count.ShouldBe(0);
    }

    [Fact]
    public async Task Draw_ReturnsWithTheRaffleRowUntouched_WhenItSucceeds()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 1);
        var before = await GetRaffleRowAsync(eventId);

        (await DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await GetRaffleRowAsync(eventId)).ShouldBe(before);
    }

    [Fact]
    public async Task Draw_ReturnsAfterStoringARedemptionCodeInTheAgreedFormat_WhenItSucceeds()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 1);

        (await DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var code = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database
            .SqlQuery<string>($"SELECT redemption_code AS \"Value\" FROM events.prize_draw WHERE prize_id = {prizes[0]}")
            .SingleAsync();
        Regex.IsMatch(code, "^RD-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$").ShouldBeTrue(code);
    }

    [Fact]
    public async Task GetRaffle_ReturnsEachPrizesStateAndDraws_WhenOneOfTwoIsDrawn()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 2);
        var drawn = (await (await DrawAsync(eventId, prizes[0])).Content.ReadFromJsonAsync<PrizeResponse>())!;

        var raffle = await _api.GetRaffleAsync(eventId);

        var all = raffle.Rounds.SelectMany(r => r.Prizes).ToDictionary(p => p.Id);
        all[prizes[0]].State.ShouldBe(PrizeState.Drawn);
        // Las entregas son una lista, que un record compara por referencia: se comparan aparte.
        IReadOnlyList<PrizeHandoverSummary> none = [];
        all[prizes[0]].Draws.Select(d => d with { Handovers = none })
            .ShouldBe(drawn.Draws.Select(d => d with { Handovers = none }));
        all[prizes[0]].Draws.ShouldAllBe(d => d.Handovers.Count == 0);
        all[prizes[1]].State.ShouldBe(PrizeState.Undrawn);
        all[prizes[1]].Draws.ShouldBeEmpty();
    }

    #endregion

    #region Concurrencia

    /// <summary>
    /// La carrera que el índice parcial no cubre (spec 02 §4): dos premios distintos, una sola persona
    /// y un premio por persona. El bloqueo del sorteo hace que el segundo vea al primero, siempre.
    /// </summary>
    [Fact]
    public async Task Draw_ReturnsOneDrawAndOnePoolExhausted_WhenTwoPrizesRaceForTheLastPerson()
    {
        for (var run = 0; run < 10; run++)
        {
            var (eventId, prizes) = await StartedRaffleAsync(people: 1, prizes: 2);

            var responses = await Task.WhenAll(DrawAsync(eventId, prizes[0]), DrawAsync(eventId, prizes[1]));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1, $"vuelta {run}");
            await RaffleTestApi.ShouldBeProblemAsync(
                responses.Single(r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "RAFFLE_POOL_EXHAUSTED");
            (await CountDrawsAsync(eventId)).ShouldBe(1);
        }
    }

    [Fact]
    public async Task Draw_ReturnsOneDrawAndOnePrizeAlreadyDrawn_WhenTheSamePrizeIsDrawnTwiceAtOnce()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 3, prizes: 1);

        var responses = await Task.WhenAll(DrawAsync(eventId, prizes[0]), DrawAsync(eventId, prizes[0]));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        await RaffleTestApi.ShouldBeProblemAsync(
            responses.Single(r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "PRIZE_ALREADY_DRAWN");
        (await CountDrawsAsync(eventId)).ShouldBe(1);
    }

    #endregion

    #region Autorización

    [Fact]
    public async Task Draw_Returns403AndWritesNothing_WhenTheCallerOnlyHoldsRafflesWrite()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 1);

        var response = await _api.SendAsync(HttpMethod.Post, DrawUrl(eventId, prizes[0]), content: null, AuthorizationPolicies.RafflesWrite);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CountDrawsAsync(eventId)).ShouldBe(0);
    }

    [Fact]
    public async Task Eligible_Returns200_WhenTheCallerOnlyHoldsPlatformStaff()
    {
        var (eventId, prizes) = await StartedRaffleAsync(people: 2, prizes: 1);

        var response = await _api.SendAsync(HttpMethod.Get, EligibleUrl(eventId, prizes[0]), content: null, "platform:staff");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    private static string DrawUrl(Guid eventId, Guid prizeId) => RaffleTestApi.DrawUrl(eventId, prizeId);

    private static string EligibleUrl(Guid eventId, Guid prizeId) => $"{RaffleTestApi.PrizesUrl(eventId)}/{prizeId}/eligible";

    private Task<HttpResponseMessage> DrawAsync(Guid eventId, Guid prizeId) => _api.DrawAsync(eventId, prizeId);

    private Task<HttpResponseMessage> EligibleAsync(Guid eventId, Guid prizeId) => _client.GetAsync(EligibleUrl(eventId, prizeId));

    private async Task<EligibleResponse> GetEligibleAsync(Guid eventId, Guid prizeId)
    {
        var response = await EligibleAsync(eventId, prizeId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<EligibleResponse>())!;
    }

    private Task<(Guid EventId, IReadOnlyList<Guid> PrizeIds)> StartedRaffleAsync(int people, int prizes) =>
        _api.StartedRaffleAsync(_factory.Attendance, people, prizes);

    private async Task<int> CountDrawsAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database
            .SqlQuery<int>(
                $"""
                SELECT count(*)::int AS "Value" FROM events.prize_draw d
                JOIN events.prize p ON p.id = d.prize_id
                JOIN events.raffle_round rr ON rr.id = p.round_id
                JOIN events.raffle r ON r.id = rr.raffle_id
                WHERE r.event_id = {eventId}
                """)
            .SingleAsync();
    }

    /// <summary>Lo que un sorteo de premio no puede mover en la fila del sorteo: su token y su autoría.</summary>
    private async Task<RaffleRow> GetRaffleRowAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database
            .SqlQuery<RaffleRow>(
                $"""
                SELECT xmin::text AS "Xmin", updated_at AS "UpdatedAt", updated_by_id AS "UpdatedById"
                FROM events.raffle WHERE event_id = {eventId}
                """)
            .SingleAsync();
    }

    private sealed record RaffleRow(string Xmin, DateTimeOffset UpdatedAt, Guid UpdatedById);
}

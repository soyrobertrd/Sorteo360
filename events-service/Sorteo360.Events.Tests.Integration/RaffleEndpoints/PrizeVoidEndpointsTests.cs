using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST …/prizes/{id}/void</c> sobre un sorteo arrancado de verdad (spec 02 §3, §7). Cada rechazo
/// comprueba que ninguna fila cambió, y la carrera contra un sorteo corre contra la base.
/// </summary>
public sealed class PrizeVoidEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public PrizeVoidEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    #region La anulación

    [Fact]
    public async Task Void_Returns200WithThePrizeUndrawnAndTheVoidedDrawKept_WhenThePrizeIsDrawn()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        var drawn = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();

        var response = await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Other, "  Se fue antes  ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var prize = (await response.Content.ReadFromJsonAsync<PrizeResponse>())!;
        prize.State.ShouldBe(PrizeState.Undrawn);
        var draw = prize.Draws.ShouldHaveSingleItem();
        draw.Id.ShouldBe(drawn.Id);
        draw.Void.ShouldNotBeNull();
        draw.Void.Code.ShouldBe(PrizeDrawVoidCode.Other);
        draw.Void.Note.ShouldBe("Se fue antes");
        var stored = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();
        stored.RedemptionCode.ShouldBe(drawn.RedemptionCode);
        stored.VoidNote.ShouldBe("Se fue antes");
        stored.VoidedAt.ShouldNotBeNull();
        stored.VoidedById.ShouldNotBeNull();
    }

    [Fact]
    public async Task Draw_IssuesADifferentCode_WhenThePrizeWasVoided()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var redraw = await _api.DrawAsync(eventId, prizes[0]);

        redraw.StatusCode.ShouldBe(HttpStatusCode.OK, await redraw.Content.ReadAsStringAsync());
        var stored = await _api.GetStoredDrawsAsync(eventId);
        stored.Count.ShouldBe(2);
        stored.Select(d => d.RedemptionCode).Distinct().Count().ShouldBe(2);
        stored.Count(d => d.VoidedAt is null).ShouldBe(1);
    }

    [Fact]
    public async Task Void_StoresANullNote_WhenTheNoteIsBlankAndTheCodeIsNotOther()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Declined, "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().VoidNote.ShouldBeNull();
    }

    [Fact]
    public async Task Void_Returns200_WhenTheEventIsClosed()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        await _api.SetEventStatusAsync(eventId, EventStatus.Closed);

        var response = await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Rechazos

    public static TheoryData<string?, string?> InvalidBodies => new()
    {
        { PrizeDrawVoidCode.Other, null },
        { PrizeDrawVoidCode.Other, "   " },
        { "lost", null },
        { null, null },
        { PrizeDrawVoidCode.Absent, new string('x', 501) },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Void_Returns400AndWritesNothing_WhenTheBodyIsInvalid(string? code, string? note)
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _api.VoidAsync(eventId, prizes[0], code, note);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().VoidedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Void_Returns200_WhenTheNoteIs500CharactersAfterTrimming()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Other, $"  {new string('x', 500)}  ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Void_Returns409PrizeNotDrawnAndChangesNothing_WhenThePrizeIsUndrawn()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 2);
        (await _api.DrawAsync(eventId, prizes[1])).StatusCode.ShouldBe(HttpStatusCode.OK);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent), HttpStatusCode.Conflict, "PRIZE_NOT_DRAWN");

        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().VoidedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Void_Returns409PrizeNotDrawn_WhenTheOnlyDrawIsAlreadyVoided()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Declined), HttpStatusCode.Conflict, "PRIZE_NOT_DRAWN");

        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().VoidCode.ShouldBe(PrizeDrawVoidCode.Absent);
    }

    [Fact]
    public async Task Void_Returns409RaffleNotInProgress_WhenTheRaffleIsADraft()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var prizeId = (await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId)))[0].Id;

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.VoidAsync(eventId, prizeId, PrizeDrawVoidCode.Absent), HttpStatusCode.Conflict, "RAFFLE_NOT_IN_PROGRESS");
    }

    [Fact]
    public async Task Void_Returns409RaffleCompletedAndChangesNothing_WhenTheRaffleIsCompleted()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        await _api.SetRaffleStateAsync(eventId, RaffleState.Completed);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent), HttpStatusCode.Conflict, "RAFFLE_COMPLETED");

        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().VoidedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Void_Returns404PrizeNotFound_WhenThePrizeIsAnotherRaffles()
    {
        var (eventId, _) = await _api.StartedRaffleAsync(_factory.Attendance, people: 1, prizes: 1);
        var (_, otherPrizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 1, prizes: 1);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.VoidAsync(eventId, otherPrizes[0], PrizeDrawVoidCode.Absent), HttpStatusCode.NotFound, "PRIZE_NOT_FOUND");
    }

    [Fact]
    public async Task Void_Returns403AndWritesNothing_WhenTheCallerOnlyHoldsRafflesWrite()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _api.SendAsync(
            HttpMethod.Post,
            RaffleTestApi.VoidUrl(eventId, prizes[0]),
            JsonContent.Create(new VoidPrizeDrawRequest { Code = PrizeDrawVoidCode.Absent }),
            AuthorizationPolicies.RafflesWrite);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().VoidedAt.ShouldBeNull();
    }

    #endregion

    #region Concurrencia

    /// <summary>
    /// Un sorteo y una anulación sobre premios distintos del mismo sorteo se ordenan en el bloqueo:
    /// ninguno escribe la fila del sorteo, así que ninguno choca contra su token.
    /// </summary>
    [Fact]
    public async Task VoidAndDraw_NeitherReturnsConcurrencyConflict_WhenTheyRaceInOneRaffle()
    {
        for (var run = 0; run < 10; run++)
        {
            var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 2);
            (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

            var responses = await Task.WhenAll(
                _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent), _api.DrawAsync(eventId, prizes[1]));

            foreach (var response in responses)
                response.StatusCode.ShouldBe(HttpStatusCode.OK, $"vuelta {run}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    /// <summary>
    /// Sobre el mismo premio: o la anulación entra antes y el sorteo vuelve a sortearlo, o el sorteo
    /// entra antes, ve al ganador vivo y la anulación lo anula. Nunca un choque.
    /// </summary>
    [Fact]
    public async Task VoidAndDraw_AnswerAsIfRunAlone_WhenTheyRaceOnTheSamePrize()
    {
        for (var run = 0; run < 10; run++)
        {
            var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 1);
            (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

            var responses = await Task.WhenAll(
                _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent), _api.DrawAsync(eventId, prizes[0]));
            var (voided, draw) = (responses[0], responses[1]);

            voided.StatusCode.ShouldBe(HttpStatusCode.OK, $"vuelta {run}: {await voided.Content.ReadAsStringAsync()}");
            if (draw.StatusCode != HttpStatusCode.OK)
                await RaffleTestApi.ShouldBeProblemAsync(draw, HttpStatusCode.Conflict, "PRIZE_ALREADY_DRAWN");
        }
    }

    #endregion
}

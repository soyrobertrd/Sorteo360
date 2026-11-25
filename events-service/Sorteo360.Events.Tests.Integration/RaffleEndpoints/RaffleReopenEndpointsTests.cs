using System.Net;
using System.Net.Http.Json;
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
/// <c>POST …/raffle/reopen</c> (spec 02 §6): la única vuelta atrás del sorteo, mientras no se sorteó
/// nada. Descarta el congelado, conserva las decisiones manuales y se ordena con el primer sorteo.
/// </summary>
public sealed class RaffleReopenEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>, IDisposable
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public RaffleReopenEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    /// <summary>La lista de DO Travel Awards es una para toda la aplicación: ningún test la deja puesta.</summary>
    public void Dispose() => _factory.Awards.ClearInForce();

    [Fact]
    public async Task Reopen_Returns200InDraftWithTheFreezeDiscardedAndOverridesKept_WhenNothingWasDrawn()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var door = FakeAttendanceSource.Invitees(3);
        _factory.Attendance.SetDoor(eventId, door);
        _factory.Awards.SetInForce(door.Select(a => a.IdentityKey).ToArray());
        (await _api.SetOverrideAsync(eventId, door[0].IdentityKey, AttendeeOverrideDecision.Excluded, "Personal"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.StartAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var startedStored = await _api.GetStoredRaffleAsync(eventId);
        startedStored.AwardsBatchId.ShouldNotBeNull();

        var response = await _api.ReopenAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var raffle = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        raffle.State.ShouldBe(RaffleState.Draft);
        raffle.StartedAt.ShouldBeNull();
        raffle.StartedById.ShouldBeNull();
        raffle.AwardsBatchId.ShouldBeNull();
        (await _api.GetStoredRaffleAsync(eventId)).ShouldBe(new StoredRaffle(RaffleState.Draft, null, null, null));
        (await _api.GetStoredAttendeesAsync(eventId)).ShouldBeEmpty();
        var kept = (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem();
        kept.Email.ShouldBe(door[0].IdentityKey);
        kept.Note.ShouldBe("Personal");
    }

    [Fact]
    public async Task Start_FreezesTheCurrentDoor_WhenTheRaffleWasReopened()
    {
        var (eventId, _) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.ReopenAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(5));

        var start = await _api.StartAsync(eventId);

        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());
        (await _api.GetStoredAttendeesAsync(eventId)).Count.ShouldBe(5);
    }

    [Fact]
    public async Task Reopen_LeavesUpdatedAtAlone_WhenItSucceeds()
    {
        var (eventId, _) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        var before = await GetUpdatedAtAsync(eventId);

        (await _api.ReopenAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await GetUpdatedAtAsync(eventId)).ShouldBe(before);
    }

    [Fact]
    public async Task Reopen_Returns409RaffleHasDrawsAndChangesNothing_WhenTheOnlyDrawIsVoided()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await RaffleTestApi.ShouldBeProblemAsync(await _api.ReopenAsync(eventId), HttpStatusCode.Conflict, "RAFFLE_HAS_DRAWS");

        (await _api.GetStoredRaffleAsync(eventId)).State.ShouldBe(RaffleState.InProgress);
        (await _api.GetStoredAttendeesAsync(eventId)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Reopen_Returns409RaffleNotInProgress_WhenTheRaffleIsADraft()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        await RaffleTestApi.ShouldBeProblemAsync(await _api.ReopenAsync(eventId), HttpStatusCode.Conflict, "RAFFLE_NOT_IN_PROGRESS");
    }

    [Fact]
    public async Task Reopen_Returns409RaffleCompleted_WhenTheRaffleIsCompleted()
    {
        var (eventId, _) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        await _api.SetRaffleStateAsync(eventId, RaffleState.Completed);

        await RaffleTestApi.ShouldBeProblemAsync(await _api.ReopenAsync(eventId), HttpStatusCode.Conflict, "RAFFLE_COMPLETED");
    }

    [Fact]
    public async Task Reopen_Returns409EventNotEditableAndChangesNothing_WhenTheEventIsClosed()
    {
        var (eventId, _) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        await _api.SetEventStatusAsync(eventId, EventStatus.Closed);

        await RaffleTestApi.ShouldBeProblemAsync(await _api.ReopenAsync(eventId), HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");

        (await _api.GetStoredRaffleAsync(eventId)).State.ShouldBe(RaffleState.InProgress);
    }

    [Fact]
    public async Task Reopen_Returns403_WhenTheCallerOnlyHoldsRafflesWrite()
    {
        var (eventId, _) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);

        var response = await _api.SendAsync(
            HttpMethod.Post, RaffleTestApi.ReopenUrl(eventId), content: null, AuthorizationPolicies.RafflesWrite);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.GetStoredRaffleAsync(eventId)).State.ShouldBe(RaffleState.InProgress);
    }

    /// <summary>
    /// Una reapertura contra el primer sorteo (spec 02 §6): o entra antes y el sorteo choca con un
    /// draft, o espera y ve el ganador. Nunca un sorteo escrito sobre un bombo borrado.
    /// </summary>
    [Fact]
    public async Task Reopen_RunsFirstOrReturnsRaffleHasDraws_WhenItRacesTheFirstDraw()
    {
        for (var run = 0; run < 10; run++)
        {
            var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);

            var responses = await Task.WhenAll(_api.ReopenAsync(eventId), _api.DrawAsync(eventId, prizes[0]));
            var (reopen, draw) = (responses[0], responses[1]);

            if (reopen.StatusCode == HttpStatusCode.OK)
            {
                await RaffleTestApi.ShouldBeProblemAsync(draw, HttpStatusCode.Conflict, "RAFFLE_NOT_IN_PROGRESS");
                (await _api.GetStoredDrawsAsync(eventId)).ShouldBeEmpty();
            }
            else
            {
                await RaffleTestApi.ShouldBeProblemAsync(reopen, HttpStatusCode.Conflict, "RAFFLE_HAS_DRAWS");
                draw.StatusCode.ShouldBe(HttpStatusCode.OK, $"vuelta {run}");
            }
        }
    }

    private async Task<DateTimeOffset> GetUpdatedAtAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database
            .SqlQuery<DateTimeOffset>($"SELECT updated_at AS \"Value\" FROM events.raffle WHERE event_id = {eventId}")
            .SingleAsync();
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>DELETE …/raffle</c>: la salida de un sorteo creado en el evento equivocado. Solo en draft,
/// donde todavía no hay nada congelado ni sorteado; después el sorteo es evidencia.
/// </summary>
public sealed class RaffleDeleteEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>, IDisposable
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleDeleteEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    /// <summary>La lista de DO Travel Awards es una para toda la aplicación: ningún test la deja puesta.</summary>
    public void Dispose() => _factory.Awards.ClearInForce();

    [Fact]
    public async Task Delete_Returns204AndRemovesTheRaffleWithItsRoundsAndPrizes_WhenItIsADraft()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), quantity: 2);

        var response = await DeleteAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await CountRowsAsync(eventId)).ShouldBe(new RaffleRowCounts(0, 0, 0, 0));
        await RaffleTestApi.ShouldBeProblemAsync(
            await _client.GetAsync(RaffleTestApi.RaffleUrl(eventId)), HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_RemovesTheKeptOverrides_WhenTheRaffleWasStartedAndReopened()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var door = FakeAttendanceSource.Invitees(2);
        _factory.Attendance.SetDoor(eventId, door);
        (await _api.SetOverrideAsync(eventId, door[0].EmailNormalized, AttendeeOverrideDecision.Excluded, "Personal"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.StartAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.ReopenAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await DeleteAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredOverridesAsync(eventId)).ShouldBeEmpty();
        (await CountRowsAsync(eventId)).ShouldBe(new RaffleRowCounts(0, 0, 0, 0));
    }

    [Fact]
    public async Task Create_Returns201_WhenTheEventsRaffleWasDeleted()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        (await DeleteAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var create = await _client.PostAsync(RaffleTestApi.RaffleUrl(eventId), content: null);

        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Sobre un completado gana el código más específico, como en toda escritura salvo la entrega
    /// (spec 00, definición de terminado).
    /// </summary>
    [Theory]
    [InlineData(RaffleState.InProgress, "RAFFLE_NOT_DRAFT")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public async Task Delete_Returns409AndChangesNothing_WhenTheRaffleLeftDraft(string state, string code)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), quantity: 1);
        var door = FakeAttendanceSource.Invitees(1);
        _factory.Attendance.SetDoor(eventId, door);
        (await _api.SetOverrideAsync(eventId, door[0].EmailNormalized, AttendeeOverrideDecision.Excluded))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _api.SetRaffleStateAsync(eventId, state);

        var response = await DeleteAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, code);
        (await CountRowsAsync(eventId)).ShouldBe(new RaffleRowCounts(1, 1, 1, 1));
    }

    [Fact]
    public async Task Delete_Returns409RaffleNotDraftAndKeepsTheDraw_WhenAPrizeWasDrawn()
    {
        var (eventId, _) = await _api.DrawnPrizeAsync(_factory.Attendance);

        var response = await DeleteAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("cancelled")]
    public async Task Delete_Returns409EventNotEditableAndChangesNothing_WhenTheEventIsTerminal(string status)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.SetEventStatusAsync(eventId, status);

        var response = await DeleteAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
        (await CountRowsAsync(eventId)).Raffles.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_Returns404RaffleNotFound_WhenTheEventHasNoRaffle()
    {
        var eventId = await _api.CreateEventAsync();

        var response = await DeleteAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_Returns403AndChangesNothing_WhenTheCallerLacksRafflesWrite()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _api.SendAsync(
            HttpMethod.Delete, RaffleTestApi.RaffleUrl(eventId), content: null, AuthorizationPolicies.RafflesDraw);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CountRowsAsync(eventId)).Raffles.ShouldBe(1);
    }

    public static TheoryData<string> DraftWrites =>
    [
        "Start", "SetOverride", "RemoveOverride", "CreateRound", "CreatePrizes",
    ];

    /// <summary>
    /// Una escritura de armado que esperaba el bloqueo del sorteo mientras otro lo eliminaba contesta
    /// que el sorteo ya no está, sin un error interno y sin dejar una ronda o un premio huérfanos.
    /// </summary>
    [Theory]
    [MemberData(nameof(DraftWrites))]
    public async Task DraftWrite_Returns404RaffleNotFoundAndWritesNothing_WhenTheRaffleIsDeletedWhileItWaits(string write)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var door = FakeAttendanceSource.Invitees(1);
        _factory.Attendance.SetDoor(eventId, door);
        var email = door[0].EmailNormalized;
        (await _api.SetOverrideAsync(eventId, email, AttendeeOverrideDecision.Excluded))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Func<Task<HttpResponseMessage>> send = write switch
        {
            "Start" => () => _api.StartAsync(eventId),
            "SetOverride" => () => _api.SetOverrideAsync(eventId, email, AttendeeOverrideDecision.Included),
            "RemoveOverride" => () => _api.RemoveOverrideAsync(eventId, email),
            "CreateRound" => () => _client.PostAsJsonAsync(
                RaffleTestApi.RoundsUrl(eventId), new CreateRaffleRoundRequest { Name = "Otra" }),
            "CreatePrizes" => () => _client.PostAsJsonAsync(
                RaffleTestApi.PrizesUrl(eventId), new CreatePrizesRequest { RoundId = roundId, Name = "Bolsa", Quantity = 1 }),
            _ => throw new ArgumentOutOfRangeException(nameof(write)),
        };

        var response = await DeletingTheRaffleWhileItWaitsAsync(eventId, send);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
        (await CountRowsAsync(eventId)).ShouldBe(new RaffleRowCounts(0, 0, 0, 0));
    }

    /// <summary>
    /// Toma el bloqueo de la fila del sorteo desde otra conexión, dispara el pedido, espera a que quede
    /// detenido en ese bloqueo, elimina el sorteo como lo elimina <c>DELETE …/raffle</c> y suelta.
    /// </summary>
    private async Task<HttpResponseMessage> DeletingTheRaffleWhileItWaitsAsync(Guid eventId, Func<Task<HttpResponseMessage>> send)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await using var transaction = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlAsync(
            $"SELECT id FROM events.raffle WHERE event_id = {eventId} FOR UPDATE");

        var pending = send();
        await _api.WaitUntilSomebodyWaitsOnALockAsync();

        await holder.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM events.prize WHERE round_id IN (
                SELECT rr.id FROM events.raffle_round rr JOIN events.raffle r ON r.id = rr.raffle_id
                WHERE r.event_id = {eventId});
            DELETE FROM events.raffle_round WHERE raffle_id IN (SELECT id FROM events.raffle WHERE event_id = {eventId});
            DELETE FROM events.raffle_attendee_override WHERE raffle_id IN (SELECT id FROM events.raffle WHERE event_id = {eventId});
            DELETE FROM events.raffle WHERE event_id = {eventId};
            """);
        await transaction.CommitAsync();

        return await pending;
    }

    private Task<HttpResponseMessage> DeleteAsync(Guid eventId) =>
        _client.DeleteAsync(RaffleTestApi.RaffleUrl(eventId));

    /// <summary>Lo que queda en la base del sorteo del evento, contado tabla por tabla.</summary>
    private async Task<RaffleRowCounts> CountRowsAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var raffleIds = context.Raffles.Where(r => r.EventId == eventId).Select(r => r.Id);
        var roundIds = context.RaffleRounds.Where(r => raffleIds.Contains(r.RaffleId)).Select(r => r.Id);

        return new RaffleRowCounts(
            await raffleIds.CountAsync(),
            await roundIds.CountAsync(),
            await context.Prizes.CountAsync(p => roundIds.Contains(p.RoundId)),
            await context.RaffleAttendeeOverrides.CountAsync(o => raffleIds.Contains(o.RaffleId)));
    }

    private sealed record RaffleRowCounts(int Raffles, int Rounds, int Prizes, int Overrides);
}

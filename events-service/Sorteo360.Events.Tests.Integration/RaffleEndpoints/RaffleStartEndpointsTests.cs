using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.Filters;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST …/raffle/start</c> con la puerta y la lista de DO Travel Awards en manos del test y el CRM
/// contestando. Cada rechazo comprueba que no se escribió nada —ni filas, ni estado, ni hora de
/// arranque—, porque un congelado a medias es el fallo que no tiene arreglo. El CRM caído va en
/// <see cref="RaffleStartCrmOutageTests"/>, y la copia de la puerta en
/// <see cref="RaffleStartCacheBypassTests"/>.
/// </summary>
public sealed class RaffleStartEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>, IDisposable
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleStartEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    /// <summary>La lista de DO Travel Awards es una para toda la aplicación: ningún test la deja puesta.</summary>
    public void Dispose() => _factory.Awards.ClearInForce();

    #region El congelado

    /// <summary>
    /// Desde A4 el padrón manda: quien llegó sin invitación, sin contacto en el CRM o con correo
    /// <c>.do</c> entra al bombo igual que cualquiera; solo la decisión manual lo saca.
    /// </summary>
    [Fact]
    public async Task Start_FreezesExactlyOneRowPerPersonAtTheDoor_WhenTheDoorIsMixed()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var door = new[]
        {
            FakeAttendanceSource.Invitee("ana@agencia.com"),
            FakeAttendanceSource.Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn },
            FakeAttendanceSource.Invitee("luis@agencia.com") with { ContactId = null },
            FakeAttendanceSource.Invitee("jose@empresa.com.do"),
            FakeAttendanceSource.Invitee("maria@agencia.com"),
        };
        _factory.Attendance.SetDoor(eventId, door);
        (await _api.SetOverrideAsync(eventId, "maria@agencia.com", AttendeeOverrideDecision.Excluded, "No vino"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await _api.StartAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        body.State.ShouldBe(RaffleState.InProgress);
        body.StartedAt.ShouldNotBeNull();
        body.StartedById.ShouldBe(TestAuthHandler.DefaultUserId);
        body.IsReadyToStart.ShouldBeNull();

        var rows = await _api.GetStoredAttendeesAsync(eventId);
        rows.Select(r => r.Email).ShouldBe(door.Select(a => a.EmailNormalized), ignoreOrder: true);
        var frozen = await _api.GetAttendeesAsync(eventId);
        (frozen.Qualified!.Value + frozen.Attendees!.Count(a => !a.Qualified)).ShouldBe(frozen.CheckedIn!.Value);
        frozen.CheckedIn.ShouldBe(door.Length);
        rows.Select(r => (r.Email, r.ExclusionReason)).ShouldBe(
        [
            ("ana@agencia.com", null),
            ("jose@empresa.com.do", null),
            ("luis@agencia.com", null),
            ("maria@agencia.com", ExclusionReason.ManuallyExcluded),
            ("pedro@agencia.com", null),
        ]);
        var maria = rows.Single(r => r.Email == "maria@agencia.com");
        maria.ManualDecision.ShouldBe(AttendeeOverrideDecision.Excluded);
        maria.ManualDecidedById.ShouldBe(TestAuthHandler.DefaultUserId);
        maria.ManualNote.ShouldBe("No vino");

        var stored = await _api.GetStoredRaffleAsync(eventId);
        stored.State.ShouldBe(RaffleState.InProgress);
        stored.StartedAt.ShouldBe(body.StartedAt);
    }

    [Fact]
    public async Task Start_Returns200WithTheWarningAndNoBatch_WhenNoAwardsListIsInForce()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await _api.StartAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        body.AwardsBatchId.ShouldBeNull();
        body.Warnings.ShouldContain(RaffleWarning.AwardsListNotInForce);
        (await _api.GetStoredRaffleAsync(eventId)).AwardsBatchId.ShouldBeNull();
        (await _api.GetRaffleAsync(eventId)).Warnings.ShouldContain(RaffleWarning.AwardsListNotInForce);
    }

    [Fact]
    public async Task Start_StampsTheBatchAndDropsTheWarning_WhenAListIsInForce()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(
            eventId, FakeAttendanceSource.Invitee("ana@agencia.com"), FakeAttendanceSource.Invitee("luis@agencia.com"));
        var batchId = _factory.Awards.SetInForce("ana@agencia.com");

        var response = await _api.StartAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredRaffleAsync(eventId)).AwardsBatchId.ShouldBe(batchId);
        (await _api.GetRaffleAsync(eventId)).Warnings.ShouldNotContain(RaffleWarning.AwardsListNotInForce);
        (await _api.GetStoredAttendeesAsync(eventId)).Single(r => r.Email == "luis@agencia.com")
            .ExclusionReason.ShouldBe(ExclusionReason.NotAwardsMember);
    }

    [Fact]
    public async Task Start_FreezesTheIncludeAsOverruled_WhenTheCrmBlacklistedThePersonAfterIt()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var vetado = FakeAttendanceSource.Invitee("vetado@agencia.com");
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"), vetado);
        (await _api.SetOverrideAsync(eventId, "vetado@agencia.com", AttendeeOverrideDecision.Included, "Lo pidió la dirección"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _factory.Crm.ServeVerdict(vetado.ContactId!.Value, "do_not_invite", "do_not_invite");

        var response = await _api.StartAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem().Email.ShouldBe("vetado@agencia.com");
        var row = (await _api.GetStoredAttendeesAsync(eventId)).Single(r => r.Email == "vetado@agencia.com");
        row.Qualified.ShouldBeFalse();
        row.ExclusionReason.ShouldBe(ExclusionReason.DoNotInvite);
        row.ManualDecision.ShouldBe(AttendeeOverrideDecision.Included);
        row.ManualNote.ShouldBe("Lo pidió la dirección");
        row.OverruledReason.ShouldBe(ExclusionReason.DoNotInvite);
    }

    #endregion

    #region Rechazos, sin escribir nada

    [Fact]
    public async Task Start_Returns409NoParticipantsAndWritesNothing_WhenTheAwardsListMatchesNobody()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        _factory.Awards.SetInForce("otra@agencia.com");

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NO_PARTICIPANTS");
        await ShouldBeUntouchedAsync(eventId);
    }

    [Fact]
    public async Task Start_Returns409NoParticipantsAndWritesNothing_WhenNobodyIsAtTheDoor()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId);

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NO_PARTICIPANTS");
        await ShouldBeUntouchedAsync(eventId);
    }

    [Fact]
    public async Task Start_Returns503AndWritesNothing_WhenTheDoorDoesNotAnswer()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.Fail(eventId);

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "ATTENDANCE_UNAVAILABLE");
        await ShouldBeUntouchedAsync(eventId);
    }

    [Fact]
    public async Task Start_Returns409TooManyRowsAndWritesNothing_WhenTheDoorIsOverTheCeiling()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(25_001));

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "ATTENDANCE_TOO_MANY_ROWS");
        await ShouldBeUntouchedAsync(eventId);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task Start_Returns409EventNotEditable_WhenTheEventIsOver(string status)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        await _api.SetEventStatusAsync(eventId, status);

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
        await ShouldBeUntouchedAsync(eventId);
    }

    [Fact]
    public async Task Start_Returns404RaffleNotFound_WhenTheEventHasNoRaffle()
    {
        var eventId = await _api.CreateEventAsync();

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    #endregion

    #region Roles

    [Fact]
    public async Task Start_Returns403_WhenTheCallerOnlyHoldsRafflesWrite()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await _api.SendAsync(
            HttpMethod.Post, RaffleTestApi.StartUrl(eventId), content: null, AuthorizationPolicies.RafflesWrite);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ShouldBeUntouchedAsync(eventId);
    }

    [Fact]
    public async Task Start_Returns200_WhenTheCallerHoldsRafflesDraw()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await _api.SendAsync(
            HttpMethod.Post, RaffleTestApi.StartUrl(eventId), content: null, AuthorizationPolicies.RafflesDraw);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Después del arranque

    [Fact]
    public async Task OverridesAndASecondStart_Return409RaffleNotDraft_WhenTheRaffleStarted()
    {
        var eventId = await StartedRaffleAsync(FakeAttendanceSource.Invitee("ana@agencia.com"));

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.SetOverrideAsync(eventId, "ana@agencia.com", AttendeeOverrideDecision.Excluded),
            HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.RemoveOverrideAsync(eventId, "ana@agencia.com"), HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        await RaffleTestApi.ShouldBeProblemAsync(await _api.StartAsync(eventId), HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        (await _api.GetStoredAttendeesAsync(eventId)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetAttendees_ReturnsTheFrozenListAndCallsNoSeam_WhenTheDoorChangedAfterTheStart()
    {
        var batchId = _factory.Awards.SetInForce("ana@agencia.com");
        var eventId = await StartedRaffleAsync(
            FakeAttendanceSource.Invitee("ana@agencia.com", lastName: "B"),
            FakeAttendanceSource.Invitee("luis@agencia.com", lastName: "A"));
        var startedAt = (await _api.GetStoredRaffleAsync(eventId)).StartedAt;
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("tarde@agencia.com"));
        _factory.Awards.ClearInForce();
        var callsBefore = _factory.Attendance.CallCount(eventId);
        _factory.Crm.ResetRequestLog();

        var response = await _client.GetAsync(RaffleTestApi.AttendeesUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<RaffleAttendeesResponse>())!;
        body.AttendanceStatus.ShouldBe(AttendanceStatus.Ok);
        body.AttendanceFetchedAt.ShouldBe(startedAt);
        body.AwardsBatchId.ShouldBe(batchId);
        body.CheckedIn.ShouldBe(2);
        body.Qualified.ShouldBe(1);
        body.Attendees.ShouldNotBeNull().Select(a => (a.Email, a.ExclusionReason)).ShouldBe(
        [
            ("luis@agencia.com", ExclusionReason.NotAwardsMember),
            ("ana@agencia.com", null),
        ]);
        response.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBeNull();
        _factory.Attendance.CallCount(eventId).ShouldBe(callsBefore);
        _factory.Crm.MailabilityBatchSizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetupWrites_Succeed_WhenTheRaffleIsInProgress()
    {
        var eventId = await StartedRaffleAsync(FakeAttendanceSource.Invitee("ana@agencia.com"));

        var roundId = await _api.CreateRoundAsync(eventId, "Ronda en curso");
        var prize = (await _api.CreatePrizesAsync(eventId, roundId, "Botella de ron")).Single();

        var editPrize = await _client.PatchAsJsonAsync(
            $"{RaffleTestApi.PrizesUrl(eventId)}/{prize.Id}",
            new UpdatePrizeRequest { RoundId = roundId, Name = "Botella de ron añejo" });
        editPrize.StatusCode.ShouldBe(HttpStatusCode.NoContent, await editPrize.Content.ReadAsStringAsync());
        var editRound = await _client.PatchAsJsonAsync(
            $"{RaffleTestApi.RoundsUrl(eventId)}/{roundId}", new UpdateRaffleRoundRequest { Name = "Ronda final" });
        editRound.StatusCode.ShouldBe(HttpStatusCode.NoContent, await editRound.Content.ReadAsStringAsync());
        (await _client.DeleteAsync($"{RaffleTestApi.PrizesUrl(eventId)}/{prize.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"{RaffleTestApi.RoundsUrl(eventId)}/{roundId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    #endregion

    #region Concurrencia

    [Fact]
    public async Task Start_FreezesOnce_WhenTwoStartsRace()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(50));

        var responses = await Task.WhenAll(_api.StartAsync(eventId), _api.StartAsync(eventId));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        await RaffleTestApi.ShouldBeProblemAsync(
            responses.Single(r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        (await _api.GetStoredAttendeesAsync(eventId)).Count.ShouldBe(50);
    }

    [Fact]
    public async Task Start_Returns409AndWritesNothing_WhenTheRaffleLeavesDraftWhileItWaitsForTheLock()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await WhileHoldingTheRaffleLockAsync(eventId, () => _api.StartAsync(eventId));

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        (await _api.GetStoredRaffleAsync(eventId)).StartedAt.ShouldBeNull();
        (await _api.GetStoredAttendeesAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetOverride_Returns409AndWritesNothing_WhenTheRaffleLeavesDraftWhileItWaitsForTheLock()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await WhileHoldingTheRaffleLockAsync(
            eventId, () => _api.SetOverrideAsync(eventId, "ana@agencia.com", AttendeeOverrideDecision.Excluded));

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        (await _api.GetStoredOverridesAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveOverride_Returns409AndKeepsTheOverride_WhenTheRaffleLeavesDraftWhileItWaitsForTheLock()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        (await _api.SetOverrideAsync(eventId, "ana@agencia.com", AttendeeOverrideDecision.Excluded))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await WhileHoldingTheRaffleLockAsync(eventId, () => _api.RemoveOverrideAsync(eventId, "ana@agencia.com"));

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem();
    }

    #endregion

    /// <summary>Un sorteo arrancado por la API, con esta puerta.</summary>
    private async Task<Guid> StartedRaffleAsync(params Attendee[] door)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, door);
        var response = await _api.StartAsync(eventId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return eventId;
    }

    /// <summary>Que el sorteo siga en draft, sin hora de arranque ni filas: el rechazo no escribió nada.</summary>
    private async Task ShouldBeUntouchedAsync(Guid eventId)
    {
        var raffle = await _api.GetStoredRaffleAsync(eventId);
        raffle.State.ShouldBe(RaffleState.Draft);
        raffle.StartedAt.ShouldBeNull();
        raffle.StartedById.ShouldBeNull();
        raffle.AwardsBatchId.ShouldBeNull();
        (await _api.GetStoredAttendeesAsync(eventId)).ShouldBeEmpty();
    }

    /// <summary>
    /// Toma el bloqueo de la fila del sorteo desde otra conexión, dispara el pedido, espera a que
    /// quede detenido en ese bloqueo, saca el sorteo de draft y suelta. Lo que el pedido conteste es
    /// lo que decidió al releer el estado bajo el bloqueo, no antes.
    /// </summary>
    private async Task<HttpResponseMessage> WhileHoldingTheRaffleLockAsync(Guid eventId, Func<Task<HttpResponseMessage>> send)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await using var transaction = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlAsync(
            $"SELECT id FROM events.raffle WHERE event_id = {eventId} FOR UPDATE");

        var pending = send();
        await _api.WaitUntilSomebodyWaitsOnALockAsync();

        await holder.Database.ExecuteSqlAsync(
            $"UPDATE events.raffle SET state = 'in_progress' WHERE event_id = {eventId}");
        await transaction.CommitAsync();

        return await pending;
    }
}

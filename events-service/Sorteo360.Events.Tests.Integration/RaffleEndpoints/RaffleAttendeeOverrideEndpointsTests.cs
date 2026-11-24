using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST</c> y <c>DELETE …/raffle/attendees/overrides</c> con la puerta en manos del test y el CRM
/// contestando. Cada rechazo comprueba además que la tabla quedó como estaba: una decisión que no
/// pasa no se escribe. El CRM caído va en <see cref="RaffleAttendeeOverrideCrmOutageTests"/>, y el
/// marcador de la puerta registrado de verdad en <see cref="RaffleEndpointsTests"/>.
/// </summary>
public sealed class RaffleAttendeeOverrideEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private const string Included = AttendeeOverrideDecision.Included;
    private const string Excluded = AttendeeOverrideDecision.Excluded;

    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleAttendeeOverrideEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region Escribir

    [Fact]
    public async Task SetOverride_Returns204AndStoresTheNormalisedEmail_WhenAnAttendeeIsExcluded()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await _api.SetOverrideAsync(eventId, "  Ana@Agencia.COM ", Excluded, note: "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var stored = (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem();
        stored.Email.ShouldBe("ana@agencia.com");
        stored.Decision.ShouldBe(Excluded);
        stored.Note.ShouldBeNull();
        stored.DecidedById.ShouldBe(TestAuthHandler.DefaultUserId);
    }

    [Fact]
    public async Task SetOverride_FlipsTheDecisionAndRestamps_WhenPostedTwice()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@sorteo360.local"));
        (await _api.SetOverrideAsync(eventId, "ana@sorteo360.local", Included, "primera")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var first = (await _api.GetStoredOverridesAsync(eventId)).Single();

        var otherUser = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, RaffleTestApi.OverridesUrl(eventId))
        {
            Content = JsonContent.Create(new { email = "ana@sorteo360.local", decision = Excluded, note = "segunda" }),
        };
        request.Headers.Add(TestAuthHandler.SubHeader, otherUser.ToString());
        (await _client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem();
        second.Decision.ShouldBe(Excluded);
        second.Note.ShouldBe("segunda");
        second.DecidedById.ShouldBe(otherUser);
        second.DecidedAt.ShouldBeGreaterThan(first.DecidedAt);
    }

    [Theory]
    [InlineData("blacklisted", "PARTICIPANT_DO_NOT_INVITE")]
    [InlineData("blacklisted_and_opted_out", "PARTICIPANT_DO_NOT_INVITE")]
    [InlineData("suspended", "PARTICIPANT_SUSPENDED")]
    public async Task SetOverride_Returns409AndWritesNothing_WhenTheIncludeNamesSomebodyThePipelineKeepsOut(
        string who, string code)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var ana = FakeAttendanceSource.Invitee("ana@agencia.com");
        _factory.Attendance.SetDoor(eventId, ana);
        if (who == "blacklisted")
            _factory.Crm.ServeVerdict(ana.ContactId!.Value, "do_not_invite", "do_not_invite");
        if (who == "blacklisted_and_opted_out")
            _factory.Crm.ServeVerdict(ana.ContactId!.Value, "opted_out", "do_not_invite");
        if (who == "suspended")
            _factory.Crm.ServeVerdict(ana.ContactId!.Value, "suspended", "suspended");

        var response = await _api.SetOverrideAsync(eventId, "ana@agencia.com", Included);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, code);
        (await _api.GetStoredOverridesAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetOverride_LeavesAnExistingIncludeInPlace_WhenARefusedIncludeTargetsTheSamePerson()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var ana = FakeAttendanceSource.Invitee("ana@sorteo360.local");
        _factory.Attendance.SetDoor(eventId, ana);
        (await _api.SetOverrideAsync(eventId, "ana@sorteo360.local", Included, "antes")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _factory.Crm.ServeVerdict(ana.ContactId!.Value, "do_not_invite", "do_not_invite");

        var response = await _api.SetOverrideAsync(eventId, "ana@sorteo360.local", Included, "después");

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "PARTICIPANT_DO_NOT_INVITE");
        (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem().Note.ShouldBe("antes");
    }

    [Theory]
    [InlineData(AttendeeOverrideDecision.Included)]
    [InlineData(AttendeeOverrideDecision.Excluded)]
    public async Task SetOverride_Returns409ParticipantNotCheckedIn_WhenTheEmailIsNotAtTheDoor(string decision)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await _api.SetOverrideAsync(eventId, "luis@agencia.com", decision);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "PARTICIPANT_NOT_CHECKED_IN");
        (await _api.GetStoredOverridesAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetOverride_Returns503AttendanceUnavailable_WhenTheLiveDoorFails()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.Fail(eventId);

        var response = await _api.SetOverrideAsync(eventId, "ana@agencia.com", Excluded);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "ATTENDANCE_UNAVAILABLE");
    }

    [Fact]
    public async Task SetOverride_Returns204_WhenAWalkInIsExcluded()
    {
        // Una exclusión nunca mete a nadie, así que no hay nada que proteger. Desde A4 el walk-in
        // califica, así que el único motivo que queda es la exclusión manual.
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn });

        var response = await _api.SetOverrideAsync(eventId, "pedro@agencia.com", Excluded);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var attendee = (await _api.GetAttendeesAsync(eventId)).Attendees.ShouldNotBeNull().ShouldHaveSingleItem();
        attendee.ExclusionReason.ShouldBe(ExclusionReason.ManuallyExcluded);
        attendee.ManualDecision.ShouldBe(Excluded);
        attendee.OverruledReason.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{ "decision": "included" }""")]
    [InlineData("""{ "email": "no-es-un-correo", "decision": "included" }""")]
    [InlineData("""{ "email": "ana@agencia.com", "decision": "maybe" }""")]
    [InlineData("""{ "email": "ana@agencia.com" }""")]
    public async Task SetOverride_Returns400_WhenTheBodyIsInvalid(string json)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.PostAsync(
            RaffleTestApi.OverridesUrl(eventId), new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetOverride_Returns400_WhenTheNoteIsOver1000Characters()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _api.SetOverrideAsync(eventId, "ana@agencia.com", Excluded, new string('x', 1001));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task SetOverride_Returns409EventNotEditable_WhenTheEventIsOver(string status)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        await _api.SetEventStatusAsync(eventId, status);

        var response = await _api.SetOverrideAsync(eventId, "ana@agencia.com", Excluded);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
    }

    [Fact]
    public async Task SetOverride_Returns404RaffleNotFound_WhenTheEventHasNone()
    {
        var eventId = await _api.CreateEventAsync();

        var response = await _api.SetOverrideAsync(eventId, "ana@agencia.com", Excluded);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task SetOverride_Returns403_WhenTheCallerLacksRafflesWrite()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _api.SendAsync(
            HttpMethod.Post,
            RaffleTestApi.OverridesUrl(eventId),
            JsonContent.Create(new { email = "ana@agencia.com", decision = Excluded }),
            roles: AuthorizationPolicies.RafflesDraw);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Retirar

    [Fact]
    public async Task RemoveOverride_Returns204BothTimes_WhenDeletedTwice()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        (await _api.SetOverrideAsync(eventId, "ana@agencia.com", Excluded)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var first = await _api.RemoveOverrideAsync(eventId, "ANA@agencia.com ");
        var second = await _api.RemoveOverrideAsync(eventId, "ana@agencia.com");

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetStoredOverridesAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveOverride_Returns204_WhenThePersonIsNoLongerAtTheDoor()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        (await _api.SetOverrideAsync(eventId, "ana@agencia.com", Excluded)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _factory.Attendance.Fail(eventId);

        var response = await _api.RemoveOverrideAsync(eventId, "ana@agencia.com");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetStoredOverridesAsync(eventId)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "email": "   " }""")]
    [InlineData("""{ "email": "no-es-un-correo" }""")]
    public async Task RemoveOverride_Returns400_WhenTheEmailIsMissingOrInvalid(string json)
    {
        // El validador corta antes del handler: el servicio nunca recibe un correo nulo por HTTP.
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, RaffleTestApi.OverridesUrl(eventId))
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveOverride_Returns403_WhenTheCallerLacksRafflesWrite()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _api.SendAsync(
            HttpMethod.Delete,
            RaffleTestApi.OverridesUrl(eventId),
            JsonContent.Create(new { email = "ana@agencia.com" }),
            roles: AuthorizationPolicies.RafflesDraw);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region De punta a punta

    [Fact]
    public async Task Overrides_ShowOnTheReadAndLoseToALaterBlacklisting_WhenTheCrmChangesItsMind()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var ana = FakeAttendanceSource.Invitee("ana@sorteo360.local");
        _factory.Attendance.SetDoor(eventId, ana);

        (await _api.SetOverrideAsync(eventId, "ana@sorteo360.local", Included, "Invitada por la ministra"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var included = (await _api.GetAttendeesAsync(eventId)).Attendees.ShouldNotBeNull().ShouldHaveSingleItem();
        included.Qualified.ShouldBeTrue();
        included.ManualDecision.ShouldBe(Included);
        included.OverruledReason.ShouldBeNull(); // calificaba igual: no venció nada
        included.ManualNote.ShouldBe("Invitada por la ministra");
        included.ManualDecidedById.ShouldBe(TestAuthHandler.DefaultUserId);
        included.ManualDecidedAt.ShouldNotBeNull();

        _factory.Crm.ServeVerdict(ana.ContactId!.Value, "do_not_invite", "do_not_invite");

        var overruled = (await _api.GetAttendeesAsync(eventId)).Attendees.ShouldNotBeNull().ShouldHaveSingleItem();
        overruled.Qualified.ShouldBeFalse();
        overruled.ExclusionReason.ShouldBe("do_not_invite");
        overruled.ManualDecision.ShouldBe(Included);
        overruled.OverruledReason.ShouldBe("do_not_invite");
        (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem().Decision.ShouldBe(Included);
    }

    #endregion
}

using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Filters;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// La lista negra del CRM en las dos lecturas del borrador, contra el CRM simulado y con el CRM
/// contestando. La caída va en <see cref="RaffleCrmOutageTests"/>, con su propio host: el circuito
/// del CRM es uno solo y abrirlo acá tumbaría el alta de eventos de los tests siguientes.
/// </summary>
public sealed class RaffleCrmGateTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleCrmGateTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task GetAttendees_ClassifiesByTheSuppressionReason_WhenTheCrmAnswers()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var vetada = FakeAttendanceSource.Invitee("vetada@agencia.com", lastName: "A");
        var desuscrita = FakeAttendanceSource.Invitee("desuscrita@agencia.com", lastName: "B");
        var borrada = FakeAttendanceSource.Invitee("borrada@agencia.com", lastName: "C");
        _factory.Attendance.SetDoor(eventId, vetada, desuscrita, borrada);
        _factory.Crm.ServeVerdict(vetada.ContactId!.Value, "do_not_invite", "do_not_invite");
        _factory.Crm.ServeVerdict(desuscrita.ContactId!.Value, "opted_out", inviteSuppressionReason: null);
        _factory.Crm.ForgetContact(borrada.ContactId!.Value);

        var response = await _client.GetAsync(RaffleTestApi.AttendeesUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBe("ok");
        response.DirectoryStatus(CrmDirectoryStatusFilter.OptHeaderName).ShouldBe("ok");
        var body = (await response.Content.ReadFromJsonAsync<RaffleAttendeesResponse>())!;
        body.Attendees.ShouldNotBeNull().Select(a => (a.Email, a.ExclusionReason)).ShouldBe(
        [
            ("vetada@agencia.com", "do_not_invite"),
            ("desuscrita@agencia.com", null),
            ("borrada@agencia.com", null), // sin respuesta del CRM no hay veto: el padrón manda (A4)
        ]);
        body.Qualified.ShouldBe(2);
    }

    [Fact]
    public async Task GetAttendees_ExcludesByTheInvitation_WhenTheEmailReasonHidesIt()
    {
        // El correo dice opted_out o no_email, que van antes; la invitación es lo que cuenta (spec 01 §3).
        var eventId = await _api.CreateEventWithRaffleAsync();
        var vetadaDesuscrita = FakeAttendanceSource.Invitee("vetada.desuscrita@agencia.com", lastName: "A");
        var vetadaPorDireccion = FakeAttendanceSource.Invitee("vetada.direccion@agencia.com", lastName: "B");
        var suspendida = FakeAttendanceSource.Invitee("suspendida@agencia.com", lastName: "C");
        var suspendidaSinCorreo = FakeAttendanceSource.Invitee("suspendida.sincorreo@agencia.com", lastName: "D");
        var desuscrita = FakeAttendanceSource.Invitee("desuscrita@agencia.com", lastName: "E");
        _factory.Attendance.SetDoor(eventId, vetadaDesuscrita, vetadaPorDireccion, suspendida, suspendidaSinCorreo, desuscrita);
        _factory.Crm.ServeVerdict(vetadaDesuscrita.ContactId!.Value, "opted_out", "do_not_invite");
        _factory.Crm.ServeVerdict(vetadaPorDireccion.ContactId!.Value, "opted_out", "do_not_invite");
        _factory.Crm.ServeVerdict(suspendida.ContactId!.Value, "suspended", "suspended");
        _factory.Crm.ServeVerdict(suspendidaSinCorreo.ContactId!.Value, "no_email", "suspended");
        _factory.Crm.ServeVerdict(desuscrita.ContactId!.Value, "opted_out", inviteSuppressionReason: null);

        var response = await _client.GetAsync(RaffleTestApi.AttendeesUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<RaffleAttendeesResponse>())!;
        body.Attendees.ShouldNotBeNull().Select(a => (a.Email, a.ExclusionReason)).ShouldBe(
        [
            ("vetada.desuscrita@agencia.com", "do_not_invite"),
            ("vetada.direccion@agencia.com", "do_not_invite"),
            ("suspendida@agencia.com", "suspended"),
            ("suspendida.sincorreo@agencia.com", "suspended"),
            ("desuscrita@agencia.com", null),
        ]);
        body.Qualified.ShouldBe(1);
    }

    [Fact]
    public async Task GetRaffle_BlocksOnCrmUnavailable_WhenTheCrmOmitsCanBeInvited()
    {
        // Un CRM anterior a PGTI-190: un 200 sin el campo es una respuesta ilegible, no un "no".
        var eventId = await _api.CreateEventWithRaffleAsync();
        var ana = FakeAttendanceSource.Invitee("ana@agencia.com");
        _factory.Attendance.SetDoor(eventId, ana);
        _factory.Crm.ServeVerdictWithoutInvitation(ana.ContactId!.Value);

        var response = await _client.GetAsync(RaffleTestApi.RaffleUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBe("unavailable");
        var body = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        body.BlockingReasons.ShouldNotBeNull().ShouldContain(RaffleBlockingReason.CrmUnavailable);
    }

    [Fact]
    public async Task GetRaffle_CarriesCrmOkAndIsReady_WhenTheCrmAnswers()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var response = await _client.GetAsync(RaffleTestApi.RaffleUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBe("ok");
        var body = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        body.BlockingReasons.ShouldNotBeNull().ShouldNotContain(RaffleBlockingReason.CrmUnavailable);
    }

    [Fact]
    public async Task GetRaffle_CarriesNoCrmHeader_WhenTheRaffleIsNotADraft()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        await _api.SetRaffleStateAsync(eventId, RaffleState.InProgress);
        _factory.Crm.ResetRequestLog();

        var response = await _client.GetAsync(RaffleTestApi.RaffleUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBeNull();
        _factory.Crm.MailabilityBatchSizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAttendees_AsksTheCrmInThreeBatches_When2500AreCheckedIn()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(2500));
        _factory.Crm.ResetRequestLog();

        var body = await _api.GetAttendeesAsync(eventId);

        body.Qualified.ShouldBe(2500);
        _factory.Crm.MailabilityBatchSizes.ShouldBe([1000, 1000, 500]);
    }
}

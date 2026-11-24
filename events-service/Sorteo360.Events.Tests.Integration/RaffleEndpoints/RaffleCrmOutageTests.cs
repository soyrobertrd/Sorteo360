using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Filters;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Las lecturas del borrador con la lista negra del CRM caída. Va aparte, con su propio host, porque
/// el circuito del CRM es uno solo: abrirlo acá dejaría sin alta de eventos a la clase que siguiera.
/// Un solo test, y el evento se crea antes de la caída, para quedar por debajo del umbral.
/// </summary>
public sealed class RaffleCrmOutageTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleCrmOutageTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task DraftReads_DegradeWithCrmUnavailable_WhenTheCrmReturns500()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(
            eventId,
            FakeAttendanceSource.Invitee("ana@agencia.com", lastName: "A"),
            FakeAttendanceSource.Invitee("pedro@agencia.com", lastName: "B") with { Origin = AttendeeOrigin.WalkIn },
            FakeAttendanceSource.Invitee("jose@empresa.com.do", lastName: "C"));
        _factory.Crm.MailabilityGoesDown();

        var raffle = await _client.GetAsync(RaffleTestApi.RaffleUrl(eventId));
        var attendees = await _client.GetAsync(RaffleTestApi.AttendeesUrl(eventId));

        raffle.StatusCode.ShouldBe(HttpStatusCode.OK);
        raffle.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBe("unavailable");
        var raffleBody = (await raffle.Content.ReadFromJsonAsync<RaffleResponse>())!;
        raffleBody.IsReadyToStart.ShouldBe(false);
        raffleBody.BlockingReasons.ShouldBe([RaffleBlockingReason.CrmUnavailable]);

        attendees.StatusCode.ShouldBe(HttpStatusCode.OK);
        attendees.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBe("unavailable");
        attendees.DirectoryStatus(CrmDirectoryStatusFilter.OptHeaderName).ShouldBe("unavailable");
        var body = (await attendees.Content.ReadFromJsonAsync<RaffleAttendeesResponse>())!;
        body.Attendees.ShouldNotBeNull().Select(a => (a.Email, a.ExclusionReason)).ShouldBe(
        [
            ("ana@agencia.com", null),
            ("pedro@agencia.com", null),
            ("jose@empresa.com.do", null),
        ]);
    }
}

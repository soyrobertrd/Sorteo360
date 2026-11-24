using System.Net;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Las decisiones manuales con la lista negra del CRM caída. Va aparte, con su propio host, por el
/// circuito del CRM (ver <see cref="RaffleCrmOutageTests"/>). Los eventos se crean antes de la caída
/// y la clase hace pocas llamadas, para quedar por debajo del umbral que lo abre.
/// </summary>
public sealed class RaffleAttendeeOverrideCrmOutageTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public RaffleAttendeeOverrideCrmOutageTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task SetOverride_Returns503CrmUnavailableAndWritesNothing_WhenTheCrmIsDownOnAnInclude()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@sorteo360.local"));
        _factory.Crm.MailabilityGoesDown();

        var response = await _api.SetOverrideAsync(eventId, "ana@sorteo360.local", AttendeeOverrideDecision.Included);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "CRM_UNAVAILABLE");
        (await _api.GetStoredOverridesAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetOverride_Returns204_WhenTheCrmIsDownOnAnExclude()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        _factory.Crm.MailabilityGoesDown();

        var response = await _api.SetOverrideAsync(eventId, "ana@agencia.com", AttendeeOverrideDecision.Excluded);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetStoredOverridesAsync(eventId)).ShouldHaveSingleItem().Decision.ShouldBe(AttendeeOverrideDecision.Excluded);
    }
}

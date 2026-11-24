using System.Net;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>start</c> con la lista negra del CRM caída. Va aparte, con su propio host, por la misma razón
/// que <see cref="RaffleCrmOutageTests"/>: el circuito del CRM es uno solo. Un solo test, y el evento
/// se crea antes de la caída.
/// </summary>
public sealed class RaffleStartCrmOutageTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public RaffleStartCrmOutageTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task Start_Returns503AndWritesNothing_WhenTheCrmReturns500()
    {
        // La lectura del borrador degrada sin la lista negra; el arranque no, porque un bombo
        // congelado sin ella no tiene arreglo.
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        _factory.Crm.MailabilityGoesDown();

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "CRM_UNAVAILABLE");
        var raffle = await _api.GetStoredRaffleAsync(eventId);
        raffle.State.ShouldBe(RaffleState.Draft);
        raffle.StartedAt.ShouldBeNull();
        (await _api.GetStoredAttendeesAsync(eventId)).ShouldBeEmpty();
    }
}

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>start</c> nunca lee la puerta de la copia de las lecturas del borrador (spec 01 §8). Va aparte
/// porque el resto de las suites del sorteo corre con la copia vigente cero segundos, y con ella
/// cualquier lectura vuelve a preguntar: acá la copia vale una hora, así que solo un arranque que la
/// salte puede ver la puerta nueva.
/// </summary>
public sealed class RaffleStartCacheBypassTests : IClassFixture<RaffleStartCacheBypassTests.CachedAttendanceApiDbFactory>
{
    private readonly CachedAttendanceApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public RaffleStartCacheBypassTests(CachedAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task Start_ReadsTheDoorLive_WhenAFreshCopyExists()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        (await _api.GetAttendeesAsync(eventId)).Attendees.ShouldNotBeNull().ShouldHaveSingleItem();

        _factory.Attendance.SetDoor(
            eventId, FakeAttendanceSource.Invitee("ana@agencia.com"), FakeAttendanceSource.Invitee("luis@agencia.com"));
        (await _api.GetAttendeesAsync(eventId)).Attendees.ShouldNotBeNull().ShouldHaveSingleItem();
        var callsBefore = _factory.Attendance.CallCount(eventId);

        var response = await _api.StartAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        _factory.Attendance.CallCount(eventId).ShouldBe(callsBefore + 1);
        (await _api.GetStoredAttendeesAsync(eventId)).Select(r => r.Email)
            .ShouldBe(["ana@agencia.com", "luis@agencia.com"]);
    }

    /// <summary>La misma aplicación, con la copia de la puerta vigente una hora.</summary>
    public sealed class CachedAttendanceApiDbFactory : RaffleAttendanceApiDbFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RaffleAttendance:CacheTtlSeconds", "3600");
        }
    }
}

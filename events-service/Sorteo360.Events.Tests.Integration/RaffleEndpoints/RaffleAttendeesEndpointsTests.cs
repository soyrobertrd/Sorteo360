using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Filters;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>GET …/raffle/attendees</c> con la puerta en manos del test. En producción la puerta es el
/// padrón (<c>RosterAttendanceSource</c>), probado en <see cref="RosterAttendanceTests"/>.
/// </summary>
public sealed class RaffleAttendeesEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleAttendeesEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    private static string AttendeesUrl(Guid eventId) => $"{RaffleTestApi.RaffleUrl(eventId)}/attendees";

    /// <summary>Walk-in, sin contacto y <c>.do</c> califican (A4); solo la exclusión manual saca a alguien.</summary>
    [Fact]
    public async Task GetAttendees_Returns200WithTheClassifiedDoorInOrder_WhenTheDoorAnswers()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(
            eventId,
            FakeAttendanceSource.Invitee("ana@empresa.com.do", "Ana", "Pérez"),
            FakeAttendanceSource.Invitee("luis@agencia.com", "Luis", "Gómez") with { Origin = AttendeeOrigin.WalkIn },
            FakeAttendanceSource.Invitee("marta@agencia.com", "Marta", "Álvarez") with { ContactId = null },
            FakeAttendanceSource.Invitee("jose@agencia.com", "José", "Báez"));
        (await _api.SetOverrideAsync(eventId, "ana@empresa.com.do", AttendeeOverrideDecision.Excluded))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await _client.GetAsync(AttendeesUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<RaffleAttendeesResponse>())!;
        body.AttendanceStatus.ShouldBe("ok");
        body.AttendanceFetchedAt.ShouldNotBeNull();
        body.AwardsBatchId.ShouldBeNull();
        body.CheckedIn.ShouldBe(4);
        body.Qualified.ShouldBe(3);

        var attendees = body.Attendees.ShouldNotBeNull();
        attendees.Select(a => (a.Email, a.ExclusionReason)).ShouldBe(
        [
            ("marta@agencia.com", null),
            ("jose@agencia.com", null),
            ("luis@agencia.com", null),
            ("ana@empresa.com.do", ExclusionReason.ManuallyExcluded),
        ]);
        attendees.Where(a => a.ManualDecision != null).Select(a => a.Email).ShouldBe(["ana@empresa.com.do"]);
        attendees.Single(a => a.Email == "jose@agencia.com").Qualified.ShouldBeTrue();
        attendees.Single(a => a.Email == "jose@agencia.com").Company.ShouldBe("Agencia");
    }

    [Fact]
    public async Task GetAttendees_CarriesNoCrmDirectoryStatusHeader_WhenNobodyAtTheDoorHasAContact()
    {
        // La asistencia es de convocatorias; esa cabecera habla solo por el CRM, y sin ningún
        // contacto que consultar no se le preguntó nada.
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com") with { ContactId = null });

        var response = await _client.GetAsync(AttendeesUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBeNull();
        response.DirectoryStatus(CrmDirectoryStatusFilter.OptHeaderName).ShouldBeNull();
    }

    [Fact]
    public async Task GetAttendees_WritesNoRows_WhenTheDoorIsRead()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(5));
        var before = await CountRowsAsync();

        var response = await _client.GetAsync(AttendeesUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CountRowsAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task GetAttendees_ReturnsThePreviousCopyAsStale_WhenTheDoorFailsAfterAGoodRead()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        var good = (await _client.GetFromJsonAsync<RaffleAttendeesResponse>(AttendeesUrl(eventId)))!;

        _factory.Attendance.Fail(eventId);
        var response = await _client.GetAsync(AttendeesUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stale = (await response.Content.ReadFromJsonAsync<RaffleAttendeesResponse>())!;
        stale.AttendanceStatus.ShouldBe("stale");
        stale.AttendanceFetchedAt.ShouldBe(good.AttendanceFetchedAt);
        stale.Attendees.ShouldNotBeNull().ShouldHaveSingleItem().Email.ShouldBe("ana@agencia.com");
    }

    [Fact]
    public async Task GetAttendees_Returns409AttendanceTooManyRows_WhenTheDoorIsOverTheCeiling()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(25_001));

        var response = await _client.GetAsync(AttendeesUrl(eventId));

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "ATTENDANCE_TOO_MANY_ROWS");
    }

    [Fact]
    public async Task GetAttendees_Returns404RaffleNotFound_WhenTheEventHasNone()
    {
        var eventId = await _api.CreateEventAsync();

        var response = await _client.GetAsync(AttendeesUrl(eventId));

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    /// <summary>
    /// Cuántas filas tiene cada tabla del esquema. Se compara el conjunto entero y no una tabla
    /// elegida, para que una escritura en cualquier parte se note.
    /// </summary>
    private async Task<Dictionary<string, long>> CountRowsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var tables = await context.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'events' AND table_type = 'BASE TABLE'")
            .ToListAsync();

        var counts = new Dictionary<string, long>();
        foreach (var table in tables)
        {
            // Una tabla no puede ir como parámetro, y el nombre sale del catálogo de la base, no de
            // un usuario: por eso se arma el texto aparte.
            var sql = $"SELECT count(*) AS \"Value\" FROM events.\"{table}\"";
            counts[table] = await context.Database.SqlQueryRaw<long>(sql).SingleAsync();
        }

        return counts;
    }
}

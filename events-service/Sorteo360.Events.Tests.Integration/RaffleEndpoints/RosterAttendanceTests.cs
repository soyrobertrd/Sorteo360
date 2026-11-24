using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// La puerta de verdad desde A4: el padrón del sorteo en Postgres (<c>RosterAttendanceSource</c>),
/// sin convocatorias ni CRM. Se importa el padrón por la API y se mira qué llega al bombo y con qué
/// <c>exclusionReason</c> lo muestra <c>GET …/participants</c>.
/// </summary>
public sealed class RosterAttendanceTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RosterAttendanceTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task GetAttendees_ReturnsOnlyTheCheckedIn_WhenTheRaffleRequiresCheckIn()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(eventId, Person("ana@banreservas.example", checkedIn: true), Person("luis@banreservas.example", checkedIn: false));

        var attendees = await _api.GetAttendeesAsync(eventId);

        attendees.AttendanceStatus.ShouldBe(AttendanceStatus.Ok);
        attendees.CheckedIn.ShouldBe(1);
        attendees.Attendees.ShouldNotBeNull().Select(a => a.Email).ShouldBe(["ana@banreservas.example"]);
        (await ReasonsAsync(eventId)).ShouldBe(new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ana@banreservas.example"] = null,
            ["luis@banreservas.example"] = "not_checked_in",
        });
    }

    [Fact]
    public async Task GetAttendees_ReturnsTheWholeRoster_WhenCheckInIsNotRequired()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(eventId, Person("ana@banreservas.example", checkedIn: true), Person("luis@banreservas.example", checkedIn: false));
        await _api.PatchRaffleAsync(eventId, RaffleTestApi.ReadyToCompleteUpdate() with { RequiresCheckIn = false });

        var attendees = await _api.GetAttendeesAsync(eventId);

        attendees.CheckedIn.ShouldBe(2);
        attendees.Qualified.ShouldBe(2);
        (await ReasonsAsync(eventId)).Values.ShouldAllBe(reason => reason == null);
    }

    [Fact]
    public async Task Start_FreezesOnlyTheCheckedInRoster_WhenTheRaffleRequiresCheckIn()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(eventId, Person("ana@banreservas.example", checkedIn: true), Person("luis@banreservas.example", checkedIn: false));

        var response = await _api.StartAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredAttendeesAsync(eventId)).Select(r => r.Email).ShouldBe(["ana@banreservas.example"]);
    }

    [Fact]
    public async Task Start_Returns409NoParticipants_WhenNobodyOnTheRosterCheckedIn()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(eventId, Person("ana@banreservas.example", checkedIn: false));

        var response = await _api.StartAsync(eventId);

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NO_PARTICIPANTS");
    }

    [Fact]
    public async Task GetParticipants_ShowsManuallyExcluded_WhenSomebodyIsTakenOutByHand()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(eventId, Person("ana@banreservas.example", checkedIn: true), Person("luis@banreservas.example", checkedIn: true));
        (await _api.SetOverrideAsync(eventId, "ana@banreservas.example", AttendeeOverrideDecision.Excluded))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ReasonsAsync(eventId))["ana@banreservas.example"].ShouldBe("manually_excluded");
        (await _api.GetAttendeesAsync(eventId)).Qualified.ShouldBe(1);
    }

    /// <summary>
    /// Después de un sorteo, el ganador queda en <c>already_won</c> y, con un premio por grupo, su
    /// compañero de departamento en <c>same_department</c>; quien no hizo check-in sigue en
    /// <c>not_checked_in</c>.
    /// </summary>
    [Fact]
    public async Task GetParticipants_ShowsAlreadyWonAndSameDepartment_WhenSomebodyWon()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await ImportAsync(eventId,
            Person("ana@banreservas.example", checkedIn: true, department: "Finanzas"),
            Person("beto@banreservas.example", checkedIn: true, department: "Finanzas"),
            Person("carla@banreservas.example", checkedIn: false, department: "Tecnología"));
        await _api.PatchRaffleAsync(eventId, RaffleTestApi.ReadyToCompleteUpdate() with { OnePrizePerCompany = true });
        var prize = (await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId)))[0].Id;
        (await _api.StartAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var draw = await _api.DrawAsync(eventId, prize);

        draw.StatusCode.ShouldBe(HttpStatusCode.OK, await draw.Content.ReadAsStringAsync());
        var winner = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().WinnerEmail;
        var colleague = winner == "ana@banreservas.example" ? "beto@banreservas.example" : "ana@banreservas.example";
        (await ReasonsAsync(eventId)).ShouldBe(new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            [winner] = "already_won",
            [colleague] = "same_department",
            ["carla@banreservas.example"] = "not_checked_in",
        });
    }

    private static RaffleParticipantInputDto Person(string email, bool checkedIn, string department = "Operaciones") =>
        new(FullName: email.Split('@')[0], Email: email, Department: department, EmployeeCode: email.Split('@')[0], CheckedIn: checkedIn);

    private async Task ImportAsync(Guid eventId, params RaffleParticipantInputDto[] people)
    {
        var response = await _client.PostAsJsonAsync(
            $"{RaffleTestApi.RaffleUrl(eventId)}/participants/import",
            new ImportRaffleParticipantsHttpRequest(people, ReplaceExisting: true, Source: "excel"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Ordenado por correo: el padrón no promete orden y la comparación sí lo mira.</summary>
    private async Task<SortedDictionary<string, string?>> ReasonsAsync(Guid eventId) =>
        new((await _client.GetFromJsonAsync<List<RaffleParticipantDto>>($"{RaffleTestApi.RaffleUrl(eventId)}/participants"))!
            .ToDictionary(p => p.Email, p => p.ExclusionReason), StringComparer.Ordinal);
}

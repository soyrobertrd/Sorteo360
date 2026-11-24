using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>isReadyToStart</c>, <c>blockingReasons</c> y <c>warnings</c> en <c>GET …/raffle</c>, con la
/// puerta en manos del test (spec 01 §8.1).
/// </summary>
public sealed class RaffleReadinessTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleReadinessTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task GetRaffle_ReturnsReadyToStart_WhenSomebodyQualifiesAndThereIsAPrize()
    {
        var eventId = await CreateRaffleWithPrizeAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var raffle = await _api.GetRaffleAsync(eventId);

        raffle.IsReadyToStart.ShouldBe(true);
        raffle.BlockingReasons.ShouldNotBeNull().ShouldBeEmpty();
        raffle.Warnings.ShouldBe(["awards_list_not_in_force"]);
    }

    [Fact]
    public async Task GetRaffle_ReturnsNoQualifiedAttendees_WhenTheDrumIsEmpty()
    {
        var eventId = await CreateRaffleWithPrizeAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("pedro@agencia.com"));
        (await _api.SetOverrideAsync(eventId, "pedro@agencia.com", AttendeeOverrideDecision.Excluded))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var raffle = await _api.GetRaffleAsync(eventId);

        raffle.IsReadyToStart.ShouldBe(false);
        raffle.BlockingReasons.ShouldBe(["no_qualified_attendees"]);
    }

    [Fact]
    public async Task GetRaffle_ReturnsTheNoPrizesWarningAndStaysReady_WhenTheRaffleHasNoPrizes()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));

        var raffle = await _api.GetRaffleAsync(eventId);

        raffle.Warnings.ShouldContain("no_prizes");
        raffle.IsReadyToStart.ShouldBe(true);
    }

    [Fact]
    public async Task GetRaffle_ReturnsAttendanceCopyStale_WhenTheDoorFailsAfterAGoodRead()
    {
        var eventId = await CreateRaffleWithPrizeAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitee("ana@agencia.com"));
        await _api.GetRaffleAsync(eventId);

        _factory.Attendance.Fail(eventId);
        var raffle = await _api.GetRaffleAsync(eventId);

        raffle.Warnings.ShouldContain("attendance_copy_stale");
        raffle.IsReadyToStart.ShouldBe(true);
    }

    [Fact]
    public async Task GetRaffle_Returns200WithAttendanceTooManyRows_WhenTheDoorIsOverTheCeiling()
    {
        var eventId = await CreateRaffleWithPrizeAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(25_001));

        var response = await _client.GetAsync(RaffleTestApi.RaffleUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var raffle = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        raffle.IsReadyToStart.ShouldBe(false);
        raffle.BlockingReasons.ShouldBe(["attendance_too_many_rows"]);
    }

    private async Task<Guid> CreateRaffleWithPrizeAsync()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        await _api.CreatePrizesAsync(eventId, roundId);
        return eventId;
    }
}

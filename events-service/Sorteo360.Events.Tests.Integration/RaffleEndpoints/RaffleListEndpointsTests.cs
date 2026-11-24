using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.GlobalEndpoints;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Las dos lecturas que cruzan sorteos: <c>GET /v1/raffles</c> y <c>GET /v1/raffles/totals</c>. Cada clase tiene su propia base, pero los tests de la clase la
/// comparten, así que se busca el sorteo propio en la lista y los totales se miden como diferencia.
/// </summary>
public sealed class RaffleListEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleListEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task ListRaffles_ReturnsTheRaffleWithItsEventAndPrizeCounts_WhenTheRaffleIsInProgress()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 3);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(eventId, prizes[1])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.HandOverAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var item = (await ListAllRafflesAsync()).Single(r => r.EventId == eventId);

        item.EventName.ShouldBe("Roadshow del sorteo");
        item.EventStartDate.ShouldBe(new DateOnly(2026, 10, 1));
        item.State.ShouldBe(RaffleState.InProgress);
        item.PrizeCount.ShouldBe(3);
        item.DrawnCount.ShouldBe(2);
        item.RedeemedCount.ShouldBe(1);
        item.StartedAt.ShouldNotBeNull();
        item.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ListRaffles_CountsOnlyTheLiveDraw_WhenAPrizeWasVoidedAndRedrawn()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 1);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var item = (await ListAllRafflesAsync()).Single(r => r.EventId == eventId);

        item.DrawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListRaffles_CountsRedeemed_OnlyWhileTheHandoverStands()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await ListAllRafflesAsync()).Single(r => r.EventId == eventId).RedeemedCount.ShouldBe(1);

        (await _api.ReverseHandoverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await ListAllRafflesAsync()).Single(r => r.EventId == eventId).RedeemedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListRaffles_OmitsTheRaffle_WhenItsEventWasDeleted()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await SoftDeleteEventAsync(eventId);

        (await ListAllRafflesAsync()).ShouldNotContain(r => r.EventId == eventId);
    }

    [Fact]
    public async Task ListRaffles_Returns400_WhenThePageSizeIsOverTheCeiling()
    {
        var response = await _client.GetAsync($"v1/raffles?pageSize={PagingLimits.MaxPageSize + 1}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTotals_CountsEachRaffleOnce_AndQualifiedOnlyFromFrozenSnapshots()
    {
        var before = await GetTotalsAsync();

        // Un borrador con gente calificada en la puerta: su bombo todavía cambia, así que no suma.
        var draftEventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(draftEventId, FakeAttendanceSource.Invitees(3));
        (await _api.GetAttendeesAsync(draftEventId)).Qualified.ShouldBe(3);
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 2);
        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.HandOverAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await GetTotalsAsync();

        (after.Raffles - before.Raffles).ShouldBe(2);
        (after.Active - before.Active).ShouldBe(1);
        (after.Prizes - before.Prizes).ShouldBe(2);
        (after.Winners - before.Winners).ShouldBe(1);
        (after.Redeemed - before.Redeemed).ShouldBe(1);
        (after.QualifiedParticipants - before.QualifiedParticipants).ShouldBe(2);
    }

    [Fact]
    public async Task GetTotals_IgnoresTheRaffle_WhenItsEventWasDeleted()
    {
        var before = await GetTotalsAsync();
        var (eventId, _) = await _api.DrawnPrizeAsync(_factory.Attendance);

        await SoftDeleteEventAsync(eventId);

        (await GetTotalsAsync()).ShouldBe(before);
    }

    private async Task<List<RaffleListItemResponse>> ListAllRafflesAsync() =>
        await ListAllAsync<RaffleListItemResponse>("v1/raffles");

    /// <summary>Recorre todas las páginas: la base de la clase la comparten todos sus tests.</summary>
    private async Task<List<T>> ListAllAsync<T>(string url)
    {
        var all = new List<T>();
        for (var page = 1; ; page++)
        {
            var response = await _client.GetAsync($"{url}?page={page}&pageSize={PagingLimits.MaxPageSize}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var paged = (await response.Content.ReadFromJsonAsync<PagedResponse<T>>())!;
            all.AddRange(paged.Items);
            if (all.Count >= paged.Total || paged.Items.Count == 0)
                return all;
        }
    }

    private async Task<RaffleTotalsResponse> GetTotalsAsync()
    {
        var response = await _client.GetAsync("v1/raffles/totals");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RaffleTotalsResponse>())!;
    }

    /// <summary>Elimina el evento directo en la base, como lo deja su propio <c>DELETE</c>.</summary>
    private async Task SoftDeleteEventAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE events.event SET deleted_at = {DateTimeOffset.UtcNow} WHERE id = {eventId}");
    }
}

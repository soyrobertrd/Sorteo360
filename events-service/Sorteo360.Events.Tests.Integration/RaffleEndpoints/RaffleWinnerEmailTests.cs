using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// El correo al ganador (spec 03 §5): uno por sorteo de premio vivo al completar, a la copia del
/// sorteo, en el idioma del evento o en español, y con su desenlace guardado en el sorteo.
/// </summary>
public sealed class RaffleWinnerEmailTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public RaffleWinnerEmailTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _factory.Notifications.ServeAccepted();
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task Complete_SendsOneEmailPerLiveDraw_WhenADrawWasVoided()
    {
        var (eventId, raffleId, liveDraws) = await TwoLiveAndOneVoidedAsync();

        await CompletedAsync(eventId);

        _factory.Notifications.SendKeys.ShouldBe(
            liveDraws.Select(d => $"events:raffle:{raffleId}:draw:{d}:won"),
            ignoreOrder: true);
        var stored = await _api.GetStoredDrawsAsync(eventId);
        stored.Single(d => d.VoidedAt is not null).WinnerNotificationStatus.ShouldBeNull();
        stored.Where(d => d.VoidedAt is null).ShouldAllBe(d => d.WinnerNotificationStatus == QuoteNotificationStatus.Sent);
    }

    [Fact]
    public async Task Complete_LeavesSentAndABatchIdOnEachLiveDraw_WhenTheEmailsGoOut()
    {
        var (eventId, _, liveDraws) = await TwoLiveAndOneVoidedAsync();

        await CompletedAsync(eventId);

        // Los correos salen después de responder (A31): el desenlace se ve al releer el sorteo.
        var draws = (await _api.GetRaffleAsync(eventId)).Rounds.SelectMany(r => r.Prizes).SelectMany(p => p.Draws).ToList();
        foreach (var draw in draws.Where(d => liveDraws.Contains(d.Id)))
        {
            draw.WinnerNotificationStatus.ShouldBe(QuoteNotificationStatus.Sent);
            Guid.TryParse(draw.WinnerNotificationId, out _).ShouldBeTrue();
        }

        var voided = draws.Single(d => d.Status == PrizeDrawStatus.Voided);
        voided.WinnerNotificationStatus.ShouldBeNull();
        voided.WinnerNotificationId.ShouldBeNull();
    }

    [Fact]
    public async Task Complete_SendsToTheSnapshotWithTheCodeAndTheInstructions_WhenAPrizeWasDrawn()
    {
        var (eventId, prizeId) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var raffleId = await RaffleIdAsync(eventId);
        _factory.Notifications.ResetRequestLog();

        await CompletedAsync(eventId);

        var draw = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();
        var send = _factory.Notifications.SingleSend;
        send.GetProperty("type").GetString().ShouldBe("events.prize.won");
        send.GetProperty("language").GetString().ShouldBe("es");
        var recipient = send.GetProperty("recipients").EnumerateArray().ShouldHaveSingleItem();
        recipient.GetProperty("email").GetString().ShouldBe(draw.WinnerEmail);
        recipient.GetProperty("name").GetString().ShouldBe(draw.WinnerName);
        send.GetProperty("about").EnumerateArray()
            .Select(a => (a.GetProperty("entityType").GetString(), a.GetProperty("entityId").GetString()))
            .ShouldBe([("event", eventId.ToString()), ("raffle", raffleId.ToString())]);
        var data = send.GetProperty("sharedData");
        data.GetProperty("codigo").GetString().ShouldBe(draw.RedemptionCode);
        data.GetProperty("instrucciones").GetString().ShouldBe(ReadyToCompleteUpdate().PickupInstructions);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("fr", "es")]
    public async Task Complete_SendsInTheEventLanguageOrSpanish_WhenTheEventHasALanguage(string eventLanguage, string sent)
    {
        var (eventId, prizeId) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await SetEventLanguageAsync(eventId, eventLanguage);
        _factory.Notifications.ResetRequestLog();

        await CompletedAsync(eventId);

        var send = _factory.Notifications.SingleSend;
        send.GetProperty("language").GetString().ShouldBe(sent);
        send.GetProperty("body").GetString().ShouldNotBeNull().ShouldContain($"<html lang=\"{sent}\">");
    }

    [Fact]
    public async Task Reissue_SendsNoEmail_WhenTheRaffleWasCompleted()
    {
        var (eventId, prizeId) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await CompletedAsync(eventId);
        _factory.Notifications.ResetRequestLog();

        var reissue = await _api.ReissueAsync(eventId, CorrectedAuthorities());

        reissue.StatusCode.ShouldBe(HttpStatusCode.OK, await reissue.Content.ReadAsStringAsync());
        _factory.Notifications.ShouldHaveReceivedNothing();
    }

    /// <summary>
    /// Tres premios sorteados y el último anulado: dos sorteos vivos y uno anulado. El registro del
    /// vecino se limpia al final, para contar solo lo que mande completar.
    /// </summary>
    private async Task<(Guid EventId, Guid RaffleId, IReadOnlyList<Guid> LiveDraws)> TwoLiveAndOneVoidedAsync()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 5, prizes: 3);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        foreach (var prizeId in prizes)
            (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[2], PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var live = (await _api.GetStoredDrawsAsync(eventId)).Where(d => d.VoidedAt is null).Select(d => d.Id).ToList();
        live.Count.ShouldBe(2);
        _factory.Notifications.ResetRequestLog();

        return (eventId, await RaffleIdAsync(eventId), live);
    }

    private async Task<RaffleResponse> CompletedAsync(Guid eventId)
    {
        var response = await _api.CompleteAsync(eventId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
    }

    private async Task<Guid> RaffleIdAsync(Guid eventId) => (await _api.GetRaffleAsync(eventId)).Id;

    private async Task SetEventLanguageAsync(Guid eventId, string language)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE events.event SET language = {language} WHERE id = {eventId}");
    }
}

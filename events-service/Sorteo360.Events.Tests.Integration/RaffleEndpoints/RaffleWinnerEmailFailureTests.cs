using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Lo que pasa cuando el correo al ganador no sale: completar sigue siendo un éxito y el sorteo de
/// premio es el único sitio donde queda dicho que nadie se enteró (spec 03 §5.1, ADR-0020).
/// <para>
/// Una clase aparte de <see cref="RaffleWinnerEmailTests"/>, con su propio host, por lo mismo que
/// <c>QuoteOutcomeNotificationFailureTests</c>: el disyuntor del chasis queda abierto después de
/// tumbar al vecino y le arruinaría el envío al test siguiente.
/// </para>
/// </summary>
public sealed class RaffleWinnerEmailFailureTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public RaffleWinnerEmailFailureTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task Complete_Returns200AndRecordsFailedOnEveryLiveDraw_WhenNotificationServiceIsDown()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 2);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        foreach (var prizeId in prizes)
            (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Notifications.GoDown();

        var response = await _api.CompleteAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        body.State.ShouldBe("completed");
        (await _api.GetStoredRaffleAsync(eventId)).State.ShouldBe("completed");

        var stored = await _api.GetStoredDrawsAsync(eventId);
        stored.Count.ShouldBe(2);
        stored.ShouldAllBe(d => d.WinnerNotificationStatus == QuoteNotificationStatus.Failed && d.WinnerNotificationId == null);
        (await _api.GetRaffleAsync(eventId)).Rounds.SelectMany(r => r.Prizes).SelectMany(p => p.Draws)
            .ShouldAllBe(d => d.WinnerNotificationStatus == QuoteNotificationStatus.Failed);
    }
}

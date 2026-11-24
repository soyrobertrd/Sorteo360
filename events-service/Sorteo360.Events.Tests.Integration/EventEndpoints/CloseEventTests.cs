using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

public sealed class CloseEventTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;

    public CloseEventTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Close_Returns200Closed_WhenInProgress()
    {
        var eventId = await CreateEventAsync();
        await StartEventAsync(eventId);

        var close = await _client.PostAsync($"v1/events/{eventId}/close", null);

        close.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await close.Content.ReadFromJsonAsync<EventDetailResponse>();
        body.ShouldNotBeNull();
        body!.Status.ShouldBe(EventStatus.Closed);

        var get = await _client.GetAsync($"v1/events/{eventId}");
        var fetched = await get.Content.ReadFromJsonAsync<EventDetailResponse>();
        fetched!.Status.ShouldBe(EventStatus.Closed);
    }

    [Fact]
    public async Task Close_Returns409_WhenEventStillPlanning()
    {
        var eventId = await CreateEventAsync();

        var close = await _client.PostAsync($"v1/events/{eventId}/close", null);

        await ShouldBeNotClosableAsync(close);
    }

    [Fact]
    public async Task Close_Returns409_WhenEventCancelled()
    {
        var eventId = await CreateEventAsync();
        await StartEventAsync(eventId);
        (await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel",
            new CancelEventRequest { Reason = "Recorte presupuestario." })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var close = await _client.PostAsync($"v1/events/{eventId}/close", null);

        await ShouldBeNotClosableAsync(close);
    }

    [Fact]
    public async Task Close_Returns409_WhenEventAlreadyClosed()
    {
        var eventId = await CreateEventAsync();
        await StartEventAsync(eventId);

        (await _client.PostAsync($"v1/events/{eventId}/close", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await _client.PostAsync($"v1/events/{eventId}/close", null);

        await ShouldBeNotClosableAsync(second);
    }

    [Fact]
    public async Task Close_Returns404_WhenEventDoesNotExist()
    {
        var close = await _client.PostAsync($"v1/events/{Guid.NewGuid()}/close", null);

        close.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #region Helpers

    private static async Task ShouldBeNotClosableAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe("EVENT_NOT_CLOSABLE");
    }

    private async Task<Guid> CreateEventAsync()
    {
        var request = new CreateEventRequest
        {
            Type      = EventType.Christmas,
            Name      = "Evento Close",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate   = new DateOnly(2026, 9, 5),
            City      = "Santo Domingo",
            Country   = "Dominican Republic"
        };
        var response = await _client.PostAsJsonAsync("v1/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task StartEventAsync(Guid eventId)
    {
        var start = await _client.PostAsync($"v1/events/{eventId}/start", null);
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion
}

using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

public sealed class EventLifecycleTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;

    public EventLifecycleTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Start

    [Fact]
    public async Task Start_Returns200InProgress_WhenPlanning()
    {
        var eventId = await CreateEventAsync();

        var start = await _client.PostAsync($"v1/events/{eventId}/start", null);

        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await start.Content.ReadFromJsonAsync<EventDetailResponse>();
        body.ShouldNotBeNull();
        body!.Status.ShouldBe(EventStatus.InProgress);

        // La transición queda persistida: un GET posterior la devuelve igual.
        var get = await _client.GetAsync($"v1/events/{eventId}");
        var fetched = await get.Content.ReadFromJsonAsync<EventDetailResponse>();
        fetched!.Status.ShouldBe(EventStatus.InProgress);
    }

    [Fact]
    public async Task Start_Returns409_WhenAlreadyInProgress()
    {
        var eventId = await CreateEventAsync();
        (await _client.PostAsync($"v1/events/{eventId}/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await _client.PostAsync($"v1/events/{eventId}/start", null);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Start_Returns409_WhenCancelled()
    {
        var eventId = await CreateEventAsync();
        await CancelEventAsync(eventId);

        var start = await _client.PostAsync($"v1/events/{eventId}/start", null);

        start.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Start_Returns404_WhenEventDoesNotExist()
    {
        var start = await _client.PostAsync($"v1/events/{Guid.NewGuid()}/start", null);

        start.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Cancel

    [Fact]
    public async Task Cancel_Returns200WithReasonPersisted_WhenPlanning()
    {
        var eventId = await CreateEventAsync();

        var cancel = await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel",
            new CancelEventRequest { Reason = "El cliente pospuso la gira." });

        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await cancel.Content.ReadFromJsonAsync<EventDetailResponse>();
        body.ShouldNotBeNull();
        body!.Status.ShouldBe(EventStatus.Cancelled);
        body.CancellationReason.ShouldBe("El cliente pospuso la gira.");

        // La razón queda persistida: un GET posterior la devuelve igual.
        var get = await _client.GetAsync($"v1/events/{eventId}");
        var fetched = await get.Content.ReadFromJsonAsync<EventDetailResponse>();
        fetched!.Status.ShouldBe(EventStatus.Cancelled);
        fetched.CancellationReason.ShouldBe("El cliente pospuso la gira.");
    }

    [Fact]
    public async Task Cancel_Returns200_WhenInProgress()
    {
        var eventId = await CreateEventAsync();
        (await _client.PostAsync($"v1/events/{eventId}/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancel = await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel",
            new CancelEventRequest { Reason = "Recorte presupuestario." });

        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await cancel.Content.ReadFromJsonAsync<EventDetailResponse>();
        body!.Status.ShouldBe(EventStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_Returns409_WhenAlreadyCancelled()
    {
        var eventId = await CreateEventAsync();
        await CancelEventAsync(eventId);

        var second = await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel",
            new CancelEventRequest { Reason = "Otra razón." });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancel_Returns400_WhenReasonMissing()
    {
        var eventId = await CreateEventAsync();

        var cancel = await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel", new CancelEventRequest());

        cancel.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_Returns404_WhenEventDoesNotExist()
    {
        var cancel = await _client.PostAsJsonAsync($"v1/events/{Guid.NewGuid()}/cancel",
            new CancelEventRequest { Reason = "No existe." });

        cancel.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Patch

    [Fact]
    public async Task Update_Returns200WithChangesPersisted_WhenPlanning()
    {
        var eventId = await CreateEventAsync();

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", new UpdateEventRequest
        {
            Name      = "Roadshow Renombrado",
            StartDate = new DateOnly(2026, 10, 10),
            EndDate    = new DateOnly(2026, 10, 15),
            Language      = "en",
            ExternalUrl  = "https://sorteo360.local/roadshow",
            InvitedCount = 80
        });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await update.Content.ReadFromJsonAsync<EventDetailResponse>();
        body.ShouldNotBeNull();
        body!.Name.ShouldBe("Roadshow Renombrado");
        body.StartDate.ShouldBe(new DateOnly(2026, 10, 10));
        body.EndDate.ShouldBe(new DateOnly(2026, 10, 15));
        body.Language.ShouldBe("en");
        body.ExternalUrl.ShouldBe("https://sorteo360.local/roadshow");
        body.InvitedCount.ShouldBe(80);
        // El type se fija al crear y nunca cambia por PATCH.
        body.Type.ShouldBe(EventType.Roadshow);
        body.Status.ShouldBe(EventStatus.Planning);

        // Los cambios quedan persistidos: un GET posterior los devuelve igual.
        var get = await _client.GetAsync($"v1/events/{eventId}");
        var fetched = await get.Content.ReadFromJsonAsync<EventDetailResponse>();
        fetched!.Name.ShouldBe("Roadshow Renombrado");
        fetched.InvitedCount.ShouldBe(80);
    }

    [Fact]
    public async Task Update_Returns200_WhenInProgress()
    {
        var eventId = await CreateEventAsync();
        (await _client.PostAsync($"v1/events/{eventId}/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest());

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_Returns409_WhenCancelled()
    {
        var eventId = await CreateEventAsync();
        await CancelEventAsync(eventId);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest());

        update.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_Returns400_WhenNameMissing()
    {
        var eventId = await CreateEventAsync();

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest() with { Name = null });

        update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Returns400_WhenEndDateBeforeStartDate()
    {
        var eventId = await CreateEventAsync();

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest() with
        {
            StartDate = new DateOnly(2026, 10, 10),
            EndDate = new DateOnly(2026, 10, 5)
        });

        update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Returns404_WhenEventDoesNotExist()
    {
        var update = await _client.PatchAsJsonAsync($"v1/events/{Guid.NewGuid()}", ValidUpdateRequest());

        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helpers

    private static UpdateEventRequest ValidUpdateRequest()
    {
        return new UpdateEventRequest
        {
            Name      = "Roadshow Editado",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate    = new DateOnly(2026, 9, 5),
            Language            = AllowedLanguage.Espanol
        };
    }

    private async Task<Guid> CreateEventAsync()
    {
        var request = new CreateEventRequest
        {
            Type      = EventType.Corporate,
            Name      = "Evento Lifecycle",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate   = new DateOnly(2026, 9, 5),
            City      = "Santo Domingo",
            Country   = "Dominican Republic"
        };
        var response = await _client.PostAsJsonAsync("v1/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task CancelEventAsync(Guid eventId)
    {
        var cancel = await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel",
            new CancelEventRequest { Reason = "Cancelación de prueba." });
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion
}

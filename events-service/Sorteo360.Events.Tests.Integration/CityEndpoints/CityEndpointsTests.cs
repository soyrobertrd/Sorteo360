using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.CityEndpoints;
using Sorteo360.Events.Web.EventEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.CityEndpoints;

public sealed class CityEndpointsTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;

    public CityEndpointsTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Removed routes

    [Fact]
    public async Task Post_IsNotRouted_BecauseAnEventHasExactlyOneCity()
    {
        // Un evento tiene exactamente una ciudad (cliente, 2026-10-06): ninguna ruta agrega otra.
        var eventId = await CreateEventAsync();

        var post = await _client.PostAsJsonAsync($"v1/events/{eventId}/cities", ValidUpdateRequest());

        post.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetDetailAsync(eventId)).Cities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_IsNotRouted_BecauseAnEventHasExactlyOneCity()
    {
        // La ruta de la ciudad solo corrige: quitarla dejaría al evento sin lugar.
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);

        var delete = await _client.DeleteAsync($"v1/events/{eventId}/cities/{cityId}");

        delete.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await GetDetailAsync(eventId)).Cities.Single().Id.ShouldBe(cityId);
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_Returns200WithChangesPersisted_WhenValid()
    {
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}", new UpdateCityRequest
        {
            Name = "La Romana",
            Country = "Dominican Republic",
            TimeZone = "America/Santo_Domingo",
            Date = new DateOnly(2026, 9, 4),
            DisplayOrder = 5
        });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await update.Content.ReadFromJsonAsync<CityResponse>();
        body.ShouldNotBeNull();
        body!.Id.ShouldBe(cityId);
        body.Name.ShouldBe("La Romana");
        body.Date.ShouldBe(new DateOnly(2026, 9, 4));
        body.DisplayOrder.ShouldBe(5);

        // Los cambios quedan persistidos: el detalle del evento los devuelve igual.
        var detail = await GetDetailAsync(eventId);
        var updated = detail.Cities.Single(c => c.Id == cityId);
        updated.Name.ShouldBe("La Romana");
        updated.DisplayOrder.ShouldBe(5);
    }

    [Fact]
    public async Task Update_ClearsDate_WhenOmitted()
    {
        // PATCH reemplaza el estado completo de la ciudad: omitir fecha la limpia,
        // igual que el PATCH del evento con sus campos opcionales.
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);
        var dated = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}",
            ValidUpdateRequest() with { Date = new DateOnly(2026, 9, 3) });
        dated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}", new UpdateCityRequest
        {
            Name = "Puerto Plata",
            Country = "Dominican Republic",
            TimeZone = "America/Santo_Domingo"
        });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await GetDetailAsync(eventId);
        detail.Cities.Single(c => c.Id == cityId).Date.ShouldBeNull();
    }

    [Fact]
    public async Task Update_CorrectsTheOneCity_WhenEventIsNotRoadshow()
    {
        // Agregar ciudades es solo del Roadshow, pero corregir la única que tiene vale para todo tipo.
        var eventId = await CreateEventAsync(EventType.Feria);
        var cityId = (await GetDetailAsync(eventId)).Cities.Single().Id;

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}",
            ValidUpdateRequest() with
            {
                Name = "Chicago", Country = "United States", TimeZone = "America/Chicago", Date = new DateOnly(2026, 9, 2)
            });

        update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        var city = (await GetDetailAsync(eventId)).Cities.Single();
        city.Name.ShouldBe("Chicago");
        city.Country.ShouldBe("United States");
        city.TimeZone.ShouldBe("America/Chicago");
        city.Date.ShouldBe(new DateOnly(2026, 9, 2));
    }

    [Fact]
    public async Task Update_Returns404_WhenCityDoesNotExist()
    {
        var eventId = await CreateEventAsync();

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{Guid.NewGuid()}", ValidUpdateRequest());

        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Returns404_WhenEventDoesNotExist()
    {
        var update = await _client.PatchAsJsonAsync($"v1/events/{Guid.NewGuid()}/cities/{Guid.NewGuid()}", ValidUpdateRequest());

        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Returns409_WhenEventCancelled()
    {
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);
        await CancelEventAsync(eventId);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}", ValidUpdateRequest());

        update.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_Returns400_WhenCountryMissing()
    {
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}", new UpdateCityRequest
        {
            Name = "La Romana"
        });

        update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ChangesTheTimeZone_AndReturnsIt()
    {
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}",
            ValidUpdateRequest() with { Name = "Chicago", TimeZone = "America/Chicago" });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<CityResponse>())!.TimeZone.ShouldBe("America/Chicago");
        var detail = await GetDetailAsync(eventId);
        detail.Cities.Single(c => c.Id == cityId).TimeZone.ShouldBe("America/Chicago");
    }

    [Fact]
    public async Task Update_Returns400_WhenTimeZoneMissing()
    {
        // El PATCH reemplaza la ciudad completa (ADR-0009): omitir la zona no la conserva.
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}",
            ValidUpdateRequest() with { TimeZone = null });

        update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("Eastern Standard Time")]
    [InlineData("")]
    public async Task Update_Returns400WithACode_WhenTheTimeZoneIsNotIana(string timeZone)
    {
        var eventId = await CreateEventAsync();
        var cityId = await OnlyCityIdAsync(eventId);

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{cityId}",
            ValidUpdateRequest() with { TimeZone = timeZone });

        await ShouldBeTimeZoneUnknownAsync(update);
    }

    #endregion

    #region Helpers

    private static UpdateCityRequest ValidUpdateRequest()
    {
        return new UpdateCityRequest
        {
            Name = "La Romana",
            Country = "Dominican Republic",
            TimeZone = "America/Santo_Domingo"
        };
    }

    private static async Task ShouldBeTimeZoneUnknownAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe("TIME_ZONE_UNKNOWN");
    }

    private async Task<Guid> CreateEventAsync(string type = EventType.Roadshow)
    {
        var request = new CreateEventRequest
        {
            Type        = type,
            Name      = "Roadshow Ciudades",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate    = new DateOnly(2026, 9, 5),
            City      = "Santo Domingo",
            Country        = "Dominican Republic"
        };
        var response = await _client.PostAsJsonAsync("v1/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> OnlyCityIdAsync(Guid eventId)
    {
        return (await GetDetailAsync(eventId)).Cities.Single().Id;
    }

    private async Task<EventDetailResponse> GetDetailAsync(Guid eventId)
    {
        var response = await _client.GetAsync($"v1/events/{eventId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EventDetailResponse>())!;
    }

    private async Task CancelEventAsync(Guid eventId)
    {
        var cancel = await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel",
            new CancelEventRequest { Reason = "Cancelación de prueba." });
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion
}

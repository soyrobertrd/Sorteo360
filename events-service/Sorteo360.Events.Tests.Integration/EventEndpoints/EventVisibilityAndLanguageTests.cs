using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

/// <summary>
/// La visibilidad del evento y su idioma ya obligatorio (spec de convocatorias <c>01</c> §1 y §2).
/// <para>
/// Lo que más se cobra acá son las dos asimetrías del PATCH, que parecen un descuido y no lo son:
/// el idioma nulo se rechaza con código, mientras que la visibilidad ausente conserva la guardada.
/// El formulario de eventos todavía no tiene control de visibilidad, así que tratarla como a los
/// demás campos opcionales despublicaría en silencio un evento anunciado cada vez que alguien le
/// corrige el nombre.
/// </para>
/// </summary>
public sealed class EventVisibilityAndLanguageTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;

    public EventVisibilityAndLanguageTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Crear

    [Fact]
    public async Task Create_DefaultsToPrivate_WhenVisibilityIsOmitted()
    {
        var eventId = await CreateEventAsync();

        var detail = await GetAsync(eventId);

        detail.Visibility.ShouldBe(EventVisibility.Private);
    }

    [Fact]
    public async Task Create_DefaultsToSpanish_WhenLanguageIsOmitted()
    {
        var eventId = await CreateEventAsync();

        var detail = await GetAsync(eventId);

        detail.Language.ShouldBe(AllowedLanguage.Espanol);
    }

    [Fact]
    public async Task Create_KeepsTheVisibilityAsked_WhenOneIsSent()
    {
        var eventId = await CreateEventAsync(visibility: EventVisibility.Public);

        var detail = await GetAsync(eventId);

        detail.Visibility.ShouldBe(EventVisibility.Public);
    }

    /// <summary>El casing lo decide quien llama; lo que se guarda es el valor canónico.</summary>
    [Fact]
    public async Task Create_StoresTheCanonicalVisibility_WhenTheCasingDiffers()
    {
        var eventId = await CreateEventAsync(visibility: "PUBLIC");

        var detail = await GetAsync(eventId);

        detail.Visibility.ShouldBe(EventVisibility.Public);
    }

    [Fact]
    public async Task Create_Returns400_WhenVisibilityIsNotOneOfTheTwo()
    {
        var response = await _client.PostAsJsonAsync("v1/events", NewEventRequest() with { Visibility = "unlisted" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Editar

    /// <summary>
    /// El único cambio de contrato que alcanza a un cliente existente. Este PATCH reemplaza la
    /// representación editable completa, así que ignorar el nulo dejaría al idioma comportándose
    /// distinto de todos los demás campos del mismo endpoint.
    /// </summary>
    [Fact]
    public async Task Update_Returns400WithACode_WhenLanguageIsNull()
    {
        var eventId = await CreateEventAsync();

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}", EditRequest() with { Language = null });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe("EVENT_LANGUAGE_REQUIRED");
    }

    /// <summary>
    /// Lo que el rechazo protege: el idioma guardado sigue ahí. Un 400 no escribe nada.
    /// </summary>
    [Fact]
    public async Task Update_LeavesTheStoredLanguage_WhenItIsRejected()
    {
        var eventId = await CreateEventAsync(language: AllowedLanguage.Ingles);

        await _client.PatchAsJsonAsync($"v1/events/{eventId}", EditRequest() with { Language = null });

        (await GetAsync(eventId)).Language.ShouldBe(AllowedLanguage.Ingles);
    }

    [Fact]
    public async Task Update_KeepsTheStoredVisibility_WhenItIsOmitted()
    {
        var eventId = await CreateEventAsync(visibility: EventVisibility.Public);

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}", EditRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAsync(eventId)).Visibility.ShouldBe(EventVisibility.Public);
    }

    /// <summary>
    /// Ni la visibilidad ni el idioma se congelan: <c>events:write</c> los cambia cuando quiera,
    /// incluso después de que una campaña haya salido (spec <c>01</c> §3).
    /// </summary>
    [Fact]
    public async Task Update_ChangesTheVisibility_WhenItIsSent()
    {
        var eventId = await CreateEventAsync(visibility: EventVisibility.Public);

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}",
            EditRequest() with { Visibility = EventVisibility.Private });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<EventDetailResponse>())!.Visibility.ShouldBe(EventVisibility.Private);
        (await GetAsync(eventId)).Visibility.ShouldBe(EventVisibility.Private);
    }

    [Fact]
    public async Task Update_Returns400_WhenVisibilityIsNotOneOfTheTwo()
    {
        var eventId = await CreateEventAsync();

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}",
            EditRequest() with { Visibility = "unlisted" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Listar

    [Fact]
    public async Task List_ReturnsOnlyThatVisibility_WhenFilteredByVisibility()
    {
        var name = $"Visibilidad {Guid.NewGuid():N}";
        var publicId = await CreateEventAsync(name: name, visibility: EventVisibility.Public);
        await CreateEventAsync(name: name, visibility: EventVisibility.Private);

        var page = await GetListAsync($"v1/events?visibility=public&searchTerm={Uri.EscapeDataString(name)}");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(publicId);
    }

    /// <summary>Sin el filtro vienen las dos, que es lo que ve una pantalla interna.</summary>
    [Fact]
    public async Task List_ReturnsBothVisibilities_WhenTheFilterIsOmitted()
    {
        var name = $"Visibilidad {Guid.NewGuid():N}";
        await CreateEventAsync(name: name, visibility: EventVisibility.Public);
        await CreateEventAsync(name: name, visibility: EventVisibility.Private);

        var page = await GetListAsync($"v1/events?searchTerm={Uri.EscapeDataString(name)}");

        page.Items.Count.ShouldBe(2);
        page.Items.Select(item => item.Visibility).ShouldBe([EventVisibility.Public, EventVisibility.Private], ignoreOrder: true);
    }

    [Fact]
    public async Task List_Returns400_WhenVisibilityIsNotOneOfTheTwo()
    {
        var response = await _client.GetAsync("v1/events?visibility=unlisted");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    private async Task<Guid> CreateEventAsync(
        string? name = null,
        string? visibility = null,
        string? language = null)
    {
        var request = NewEventRequest() with
        {
            Name = name ?? "Roadshow Visibilidad",
            Visibility = visibility,
            Language = language
        };

        var response = await _client.PostAsJsonAsync("v1/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static CreateEventRequest NewEventRequest() => new()
    {
        Type        = EventType.Roadshow,
        Name        = "Roadshow Visibilidad",
        StartDate   = new DateOnly(2027, 4, 1),
        EndDate     = new DateOnly(2027, 4, 5),
        City        = "Santo Domingo",
        Country     = "Dominican Republic"
    };

    private static UpdateEventRequest EditRequest() => new()
    {
        Name        = "Roadshow Visibilidad (editado)",
        StartDate   = new DateOnly(2027, 4, 1),
        EndDate     = new DateOnly(2027, 4, 5),
        Language    = AllowedLanguage.Espanol
    };

    private async Task<EventDetailResponse> GetAsync(Guid eventId)
    {
        var response = await _client.GetAsync($"v1/events/{eventId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EventDetailResponse>())!;
    }

    private async Task<PagedResponse<EventListItemResponse>> GetListAsync(string route)
    {
        var response = await _client.GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PagedResponse<EventListItemResponse>>())!;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

/// <summary>Prueba el montaje del salón del evento a través de la API HTTP.</summary>
/// <remarks>
/// Cubre que el montaje del salón se guarde al crear, se lea en el detalle y en el listado, se
/// cambie y se borre al editar, y qué valores rechaza el servicio. Es opcional para todo tipo de
/// evento.
/// </remarks>
public sealed class EventSeatingLayoutTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;

    public EventSeatingLayoutTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Alta y lectura

    public static TheoryData<string> AllLayouts() => new(SeatingLayout.All);

    /// <summary>Cada uno de los seis montajes del salón se guarda y se lee tal cual.</summary>
    /// <remarks>
    /// Recorre <see cref="SeatingLayout.All"/> contra la base real, así que también prueba que el
    /// check <c>ck_event_seating_layout</c> acepta los seis. Si alguien agrega un montaje a la clase
    /// y no a la migración, este test falla con un 500 en vez de dejarlo llegar a producción.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllLayouts))]
    public async Task Create_StoresTheLayout_ForEveryLayout(string layout)
    {
        var eventId = await CreateEventAsync(NewEventRequest() with { SeatingLayout = layout });

        var detail = await GetDetailAsync(eventId);

        detail.SeatingLayout.ShouldBe(layout);
    }

    /// <summary>Un evento sin montaje del salón se crea igual, con el campo nulo.</summary>
    /// <remarks>Nulo quiere decir que nadie lo eligió todavía, no que el evento sea un cóctel.</remarks>
    [Fact]
    public async Task Create_LeavesTheLayoutNull_WhenItIsOmitted()
    {
        var eventId = await CreateEventAsync(NewEventRequest());

        var detail = await GetDetailAsync(eventId);

        detail.SeatingLayout.ShouldBeNull();
    }

    /// <summary>El servicio guarda el valor canónico aunque quien llama use otras mayúsculas.</summary>
    [Fact]
    public async Task Create_StoresTheCanonicalLayout_WhenTheCasingDiffers()
    {
        var eventId = await CreateEventAsync(NewEventRequest() with { SeatingLayout = "uSHAPE" });

        var detail = await GetDetailAsync(eventId);

        detail.SeatingLayout.ShouldBe(SeatingLayout.UShape);
    }

    /// <summary>Un valor fuera de los seis es un error de campo sin código.</summary>
    /// <remarks>
    /// Así se rechazan también la visibilidad y el tipo. El formulario elige el montaje del salón
    /// de una lista y nunca manda un valor así, de modo que no hay mensaje que traducir.
    /// </remarks>
    [Fact]
    public async Task Create_Returns400OnTheField_WhenTheLayoutIsNotOneOfTheSix()
    {
        var response = await _client.PostAsJsonAsync("v1/events", NewEventRequest() with { SeatingLayout = "Theater" });

        await AssertFieldRejectedAsync(response);
    }

    #endregion

    #region Edición

    [Fact]
    public async Task Update_ChangesTheLayout_WhenANewOneIsSent()
    {
        var eventId = await CreateEventAsync(NewEventRequest() with { SeatingLayout = SeatingLayout.Banquet });

        var response = await _client.PatchAsJsonAsync(
            $"v1/events/{eventId}", EditRequest() with { SeatingLayout = SeatingLayout.Classroom });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<EventDetailResponse>())!.SeatingLayout.ShouldBe(SeatingLayout.Classroom);
        (await GetDetailAsync(eventId)).SeatingLayout.ShouldBe(SeatingLayout.Classroom);
    }

    /// <summary>Un PATCH que omite el montaje del salón lo borra.</summary>
    /// <remarks>
    /// El PATCH reemplaza la representación editable completa (ADR-0009). La visibilidad es la
    /// única excepción a esa regla, y el montaje del salón no lo es.
    /// </remarks>
    [Fact]
    public async Task Update_ClearsTheLayout_WhenItIsOmitted()
    {
        var eventId = await CreateEventAsync(NewEventRequest() with { SeatingLayout = SeatingLayout.Banquet });

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}", EditRequest(), OmitNulls);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GetDetailAsync(eventId)).SeatingLayout.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ClearsTheLayout_WhenItIsSentAsNull()
    {
        var eventId = await CreateEventAsync(NewEventRequest() with { SeatingLayout = SeatingLayout.Banquet });

        var response = await _client.PatchAsJsonAsync(
            $"v1/events/{eventId}", EditRequest() with { SeatingLayout = null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GetDetailAsync(eventId)).SeatingLayout.ShouldBeNull();
    }

    /// <summary>Un PATCH rechazado no escribe nada, así que el montaje del salón guardado sigue igual.</summary>
    [Fact]
    public async Task Update_Returns400OnTheFieldAndKeepsTheStoredLayout_WhenTheLayoutIsNotOneOfTheSix()
    {
        var eventId = await CreateEventAsync(NewEventRequest() with { SeatingLayout = SeatingLayout.Banquet });

        var response = await _client.PatchAsJsonAsync(
            $"v1/events/{eventId}", EditRequest() with { SeatingLayout = "Theater" });

        await AssertFieldRejectedAsync(response);
        (await GetDetailAsync(eventId)).SeatingLayout.ShouldBe(SeatingLayout.Banquet);
    }

    #endregion

    #region Listar

    /// <summary>Cada fila del listado trae su montaje del salón, y la que no tiene uno lo trae nulo.</summary>
    [Fact]
    public async Task List_ReturnsEachEventsLayout_IncludingNone()
    {
        var name = $"Montaje {Guid.NewGuid():N}";
        var withLayout = await CreateEventAsync(NewEventRequest() with { Name = name, SeatingLayout = SeatingLayout.UShape });
        var withoutLayout = await CreateEventAsync(NewEventRequest() with { Name = name });

        var response = await _client.GetAsync($"v1/events?searchTerm={Uri.EscapeDataString(name)}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<EventListItemResponse>>())!;

        page.Items.Count.ShouldBe(2);
        page.Items.Single(item => item.Id == withLayout).SeatingLayout.ShouldBe(SeatingLayout.UShape);
        page.Items.Single(item => item.Id == withoutLayout).SeatingLayout.ShouldBeNull();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Serializa sin las propiedades nulas, para que la clave falte del cuerpo como falta cuando
    /// edita el formulario de hoy, que no manda el montaje del salón.
    /// </summary>
    private static readonly JsonSerializerOptions OmitNulls = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static CreateEventRequest NewEventRequest() =>
        new()
        {
            Type        = EventType.Roadshow,
            Name        = "Roadshow Montaje",
            StartDate   = new DateOnly(2026, 9, 1),
            EndDate     = new DateOnly(2026, 9, 5),
            City        = "Madrid",
            Country     = "Spain"
        };

    private static UpdateEventRequest EditRequest() =>
        new()
        {
            Name        = "Roadshow Montaje",
            StartDate   = new DateOnly(2026, 9, 1),
            EndDate     = new DateOnly(2026, 9, 5),
            Language    = AllowedLanguage.Espanol
        };

    private async Task<Guid> CreateEventAsync(CreateEventRequest request)
    {
        var response = await _client.PostAsJsonAsync("v1/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<EventDetailResponse> GetDetailAsync(Guid eventId)
    {
        var response = await _client.GetAsync($"v1/events/{eventId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EventDetailResponse>();
        body.ShouldNotBeNull();
        return body!;
    }

    private static async Task AssertFieldRejectedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        problem.ShouldNotBeNull();
        problem!.Errors.Keys.ShouldContain(nameof(CreateEventRequest.SeatingLayout));
        problem.Extensions.ShouldNotContainKey("code");
    }

    #endregion
}

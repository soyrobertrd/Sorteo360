using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

/// <summary>
/// El presupuesto estimado y la descripción del evento: que se guarden al crear, se lean al leer,
/// se editen y se borren, y qué rechaza el servicio.
/// </summary>
public sealed class EventBudgetAndDescriptionTests : IClassFixture<EventsApiDbFactory>
{
    private const string Description =
        "Roadshow de promoción turística por tres ciudades europeas, con agenda de citas B2B.";

    private readonly HttpClient _client;

    public EventBudgetAndDescriptionTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Alta y lectura

    [Fact]
    public async Task Create_ReturnsBothFieldsOnDetail_WhenProvided()
    {
        var eventId = await CreateEventAsync(ValidRequest() with
        {
            Description     = Description,
            EstimatedBudget = new MoneyRequest { Amount = 125_000.50m, Currency = "EUR" }
        });

        var detail = await GetDetailAsync(eventId);

        detail.Description.ShouldBe(Description);
        detail.EstimatedBudget.ShouldNotBeNull();
        detail.EstimatedBudget!.Amount.ShouldBe(125_000.50m);
        detail.EstimatedBudget.Currency.ShouldBe("EUR");
    }

    [Fact]
    public async Task Create_LeavesBothNull_WhenOmitted()
    {
        // Los dos campos son opcionales: un evento puede planificarse antes de tener cifra ni texto.
        var eventId = await CreateEventAsync(ValidRequest());

        var detail = await GetDetailAsync(eventId);

        detail.Description.ShouldBeNull();
        detail.EstimatedBudget.ShouldBeNull();
    }

    [Fact]
    public async Task Create_KeepsBudgetApartFromTotalCost_WhenEventIsNew()
    {
        // Presupuestado y gastado son dos cosas distintas: el costo total se materializa al cerrar
        // y no lo toca lo que la OPT haya presupuestado.
        var eventId = await CreateEventAsync(ValidRequest() with
        {
            EstimatedBudget = new MoneyRequest { Amount = 9_000m, Currency = "USD" }
        });

        var detail = await GetDetailAsync(eventId);

        detail.EstimatedBudget!.Amount.ShouldBe(9_000m);
        detail.TotalCost.ShouldBeNull();
    }

    #endregion

    #region Edición

    [Fact]
    public async Task Update_ReplacesBothFields_WhenBothAreSent()
    {
        var eventId = await CreateEventAsync(ValidRequest() with
        {
            Description     = Description,
            EstimatedBudget = new MoneyRequest { Amount = 1_000m, Currency = "USD" }
        });

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest() with
        {
            Description     = "Agenda reducida a dos ciudades.",
            EstimatedBudget = new MoneyRequest { Amount = 750.25m, Currency = "EUR" }
        });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetailAsync(eventId);
        detail.Description.ShouldBe("Agenda reducida a dos ciudades.");
        detail.EstimatedBudget!.Amount.ShouldBe(750.25m);
        detail.EstimatedBudget.Currency.ShouldBe("EUR");
    }

    [Fact]
    public async Task Update_ClearsBothFields_WhenOmitted()
    {
        // El PATCH reemplaza la representación editable completa, así que omitir un campo opcional
        // lo borra en vez de conservarlo.
        var eventId = await CreateEventAsync(ValidRequest() with
        {
            Description     = Description,
            EstimatedBudget = new MoneyRequest { Amount = 1_000m, Currency = "USD" }
        });

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest());

        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetailAsync(eventId);
        detail.Description.ShouldBeNull();
        detail.EstimatedBudget.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ClearsOnlyTheBudget_WhenDescriptionIsKept()
    {
        var eventId = await CreateEventAsync(ValidRequest() with
        {
            Description     = Description,
            EstimatedBudget = new MoneyRequest { Amount = 1_000m, Currency = "USD" }
        });

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest() with
        {
            Description = Description
        });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetailAsync(eventId);
        detail.EstimatedBudget.ShouldBeNull();
        detail.Description.ShouldBe(Description);
    }

    [Fact]
    public async Task Update_ClearsOnlyTheDescription_WhenBudgetIsKept()
    {
        var eventId = await CreateEventAsync(ValidRequest() with
        {
            Description     = Description,
            EstimatedBudget = new MoneyRequest { Amount = 1_000m, Currency = "USD" }
        });

        var update = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest() with
        {
            EstimatedBudget = new MoneyRequest { Amount = 1_000m, Currency = "USD" }
        });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetailAsync(eventId);
        detail.Description.ShouldBeNull();
        detail.EstimatedBudget!.Amount.ShouldBe(1_000m);
    }

    #endregion

    #region Rechazos

    [Theory]
    [InlineData("DOP")]
    [InlineData("usd")]
    [InlineData("0")]
    public async Task Create_Returns400_WhenBudgetCurrencyIsNotSupported(string currency)
    {
        var response = await _client.PostAsJsonAsync("v1/events", ValidRequest() with
        {
            EstimatedBudget = new MoneyRequest { Amount = 1_000m, Currency = currency }
        });

        await AssertProblemAsync(response, "EVENT_BUDGET_CURRENCY_NOT_SUPPORTED");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Create_Returns400_WhenBudgetAmountIsNotPositive(int amount)
    {
        var response = await _client.PostAsJsonAsync("v1/events", ValidRequest() with
        {
            EstimatedBudget = new MoneyRequest { Amount = amount, Currency = "USD" }
        });

        await AssertProblemAsync(response, "EVENT_BUDGET_AMOUNT_INVALID");
    }

    [Fact]
    public async Task Update_Returns400_WhenBudgetCurrencyIsNotSupported()
    {
        var eventId = await CreateEventAsync(ValidRequest());

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest() with
        {
            EstimatedBudget = new MoneyRequest { Amount = 1_000m, Currency = "DOP" }
        });

        await AssertProblemAsync(response, "EVENT_BUDGET_CURRENCY_NOT_SUPPORTED");
    }

    [Fact]
    public async Task Update_Returns400_WhenBudgetAmountIsNegative()
    {
        var eventId = await CreateEventAsync(ValidRequest());

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}", ValidUpdateRequest() with
        {
            EstimatedBudget = new MoneyRequest { Amount = -50m, Currency = "USD" }
        });

        await AssertProblemAsync(response, "EVENT_BUDGET_AMOUNT_INVALID");
    }

    #endregion

    #region Helpers

    private static CreateEventRequest ValidRequest() =>
        new()
        {
            Type        = EventType.Roadshow,
            Name        = "Roadshow Presupuesto",
            StartDate   = new DateOnly(2026, 9, 1),
            EndDate     = new DateOnly(2026, 9, 5),
            City        = "Madrid",
            Country     = "Spain"
        };

    private static UpdateEventRequest ValidUpdateRequest() =>
        new()
        {
            Name        = "Roadshow Presupuesto",
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

    private static async Task AssertProblemAsync(HttpResponseMessage response, string expectedCode)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem!.Extensions["code"]!.ToString().ShouldBe(expectedCode);
    }

    #endregion
}

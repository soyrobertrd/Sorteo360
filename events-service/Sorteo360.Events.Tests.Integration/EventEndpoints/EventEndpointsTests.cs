using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

public sealed class EventEndpointsTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly EventsApiDbFactory _factory;

    public EventEndpointsTests(EventsApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region POST /v1/events

    [Fact]
    public async Task CreateEvent_Returns201WithLocation_WhenRequestIsValid()
    {
        var response = await _client.PostAsJsonAsync("v1/events", CreateValidRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain("/v1/events/");
    }

    [Fact]
    public async Task CreateEvent_Returns400_WhenTypeIsInvalid()
    {
        var request = CreateValidRequest() with { Type = "TipoInvalido" };

        var response = await _client.PostAsJsonAsync("v1/events", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_Returns400_WhenNameIsEmpty()
    {
        var request = CreateValidRequest() with { Name = "" };

        var response = await _client.PostAsJsonAsync("v1/events", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_Returns400_WhenEndDateIsBeforeStartDate()
    {
        var request = CreateValidRequest() with
        {
            StartDate = new DateOnly(2026, 9, 10),
            EndDate   = new DateOnly(2026, 9, 5)
        };

        var response = await _client.PostAsJsonAsync("v1/events", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_Returns201_WhenRequestIsMinimal()
    {
        var request = CreateValidRequest();

        var response = await _client.PostAsJsonAsync("v1/events", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateEvent_UsesDefaultLocation_WhenCityAndCountryAreEmpty()
    {
        var request = CreateValidRequest() with { City = "", Country = "" };

        var response = await _client.PostAsJsonAsync("v1/events", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = await _client.GetFromJsonAsync<EventDetailResponse>(response.Headers.Location!.ToString());
        detail!.Cities.Single().Name.ShouldBe("Santo Domingo");
        detail.Cities.Single().Country.ShouldBe("República Dominicana");
    }

    [Fact]
    public async Task CreateEvent_GivesTheFirstCityTheTimeZone_WhenOneIsSent()
    {
        var create = await _client.PostAsJsonAsync("v1/events",
            CreateValidRequest() with { City = "Chicago", Country = "United States", TimeZone = "America/Chicago" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var detail = await _client.GetFromJsonAsync<EventDetailResponse>(create.Headers.Location!.ToString());

        detail!.Cities.Single().TimeZone.ShouldBe("America/Chicago");
    }

    [Fact]
    public async Task CreateEvent_GivesTheFirstCitySantoDomingoTime_WhenNoTimeZoneIsSent()
    {
        var create = await _client.PostAsJsonAsync("v1/events", CreateValidRequest());
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var detail = await _client.GetFromJsonAsync<EventDetailResponse>(create.Headers.Location!.ToString());

        detail!.Cities.Single().TimeZone.ShouldBe("America/Santo_Domingo");
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("Central Standard Time")]
    public async Task CreateEvent_Returns400WithACode_WhenTheTimeZoneIsNotIana(string timeZone)
    {
        var response = await _client.PostAsJsonAsync("v1/events", CreateValidRequest() with { TimeZone = timeZone });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe("TIME_ZONE_UNKNOWN");
    }

    #endregion

    #region GET /v1/events/{id}

    [Fact]
    public async Task GetEventById_Returns200WithBody_WhenEventExists()
    {
        var createRequest = CreateValidRequest();
        var createResponse = await _client.PostAsJsonAsync("v1/events", createRequest);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var location = createResponse.Headers.Location!.ToString();
        var response = await _client.GetAsync(location);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EventDetailResponse>();
        body.ShouldNotBeNull();
        body!.Type.ShouldBe(createRequest.Type);
        body.Name.ShouldBe(createRequest.Name);
        body.Status.ShouldBe(EventStatus.Planning);
        body.Cities.Count.ShouldBe(1);
        body.Cities[0].Name.ShouldBe(createRequest.City);
        body.Cities[0].Country.ShouldBe(createRequest.Country);
    }

    [Fact]
    public async Task GetEventById_Returns404_WhenEventDoesNotExist()
    {
        var response = await _client.GetAsync($"v1/events/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /v1/events (list)

    [Fact]
    public async Task ListEvents_Returns200WithAPagedEnvelopeContainingTheSeeded_WhenEventsExist()
    {
        // Sin filtros no se puede afirmar el total global (la tabla la comparten otros tests),
        // así que se verifica la envoltura y que el evento sembrado esté presente. Se siembra
        // en el año más lejano para garantizar que cae en la primera página (orden StartDate desc).
        var id = Guid.NewGuid();
        await SeedEventAsync(id, EventType.Roadshow, EventStatus.Planning, new DateOnly(2099, 6, 1));

        var response = await _client.GetAsync("v1/events");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<EventListItemResponse>>();
        body.ShouldNotBeNull();
        body!.Page.ShouldBe(1);
        body.PageSize.ShouldBe(20);
        body.Total.ShouldBeGreaterThanOrEqualTo(1);
        body.Items.ShouldContain(e => e.Id == id);
    }

    [Fact]
    public async Task ListEvents_ReturnsOnlyThatTypeWithAnExactTotal_WhenFilteredByType()
    {
        var (from, to) = Window(2081);
        await SeedEventAsync(Guid.NewGuid(), EventType.Corporate, EventStatus.Planning, new DateOnly(2081, 3, 1));
        await SeedEventAsync(Guid.NewGuid(), EventType.Christmas, EventStatus.Planning, new DateOnly(2081, 6, 1));

        var body = await GetListAsync($"v1/events?type=Corporate&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(1);
        body.Items.ShouldAllBe(e => e.Type == EventType.Corporate);
    }

    [Fact]
    public async Task ListEvents_ReturnsOnlyThatStatusWithAnExactTotal_WhenFilteredByStatus()
    {
        var (from, to) = Window(2082);
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Planning, new DateOnly(2082, 3, 1));
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Closed, new DateOnly(2082, 6, 1));

        var body = await GetListAsync($"v1/events?status=closed&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(1);
        body.Items.ShouldAllBe(e => e.Status == EventStatus.Closed);
    }

    [Fact]
    public async Task ListEvents_ExcludesClosedAndCancelled_WhenActiveIsTrue()
    {
        var (from, to) = Window(2083);
        await SeedWindowWithAllStatusesAsync(2083);

        var body = await GetListAsync($"v1/events?active=true&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(2);
        body.Items.ShouldAllBe(e => e.Status == EventStatus.Planning || e.Status == EventStatus.InProgress);
    }

    [Fact]
    public async Task ListEvents_ReturnsOnlyClosedAndCancelled_WhenActiveIsFalse()
    {
        var (from, to) = Window(2084);
        await SeedWindowWithAllStatusesAsync(2084);

        var body = await GetListAsync($"v1/events?active=false&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(2);
        body.Items.ShouldAllBe(e => e.Status == EventStatus.Closed || e.Status == EventStatus.Cancelled);
    }

    [Fact]
    public async Task ListEvents_MatchesTheTypeIgnoringCase_WhenFilteredByType()
    {
        var (from, to) = Window(2085);
        await SeedEventAsync(Guid.NewGuid(), EventType.Corporate, EventStatus.Planning, new DateOnly(2085, 3, 1));

        var body = await GetListAsync($"v1/events?type=corporate&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(1);
        body.Items.ShouldAllBe(e => e.Type == EventType.Corporate);
    }

    [Fact]
    public async Task ListEvents_IgnoresTheTypeFilter_WhenTheTypeParamIsEmpty()
    {
        var (from, to) = Window(2086);
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Planning, new DateOnly(2086, 3, 1));
        await SeedEventAsync(Guid.NewGuid(), EventType.Feria, EventStatus.Planning, new DateOnly(2086, 6, 1));

        var body = await GetListAsync($"v1/events?type=&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(2);
    }

    [Fact]
    public async Task ListEvents_ReturnsOnlyEventsInTheRange_WhenFilteredByStartDate()
    {
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Planning, new DateOnly(2087, 1, 10));
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Planning, new DateOnly(2087, 6, 10));
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Planning, new DateOnly(2087, 12, 10));

        var body = await GetListAsync("v1/events?startDateFrom=2087-05-01&startDateTo=2087-07-01");

        body.Total.ShouldBe(1);
        body.Items.Single().StartDate.ShouldBe(new DateOnly(2087, 6, 10));
    }

    [Fact]
    public async Task ListEvents_ReturnsDeterministicNonOverlappingSlices_WhenPaging()
    {
        var (from, to) = Window(2088);
        // Misma StartDate y mismo CreatedAt para forzar el desempate por Id.
        var sameDate = new DateOnly(2088, 5, 5);
        var sameInstant = new DateTimeOffset(2088, 5, 5, 12, 0, 0, TimeSpan.Zero);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids)
            await SeedEventAsync(id, EventType.Roadshow, EventStatus.Planning, sameDate, sameInstant);

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var body = await GetListAsync($"v1/events?startDateFrom={from}&startDateTo={to}&page={page}&pageSize=1");
            body.Total.ShouldBe(3);
            body.Items.Count.ShouldBe(1);
            seen.Add(body.Items.Single().Id);
        }

        // Las tres páginas devuelven ids distintos que cubren exactamente el conjunto sembrado.
        seen.Distinct().Count().ShouldBe(3);
        seen.ShouldBe(ids, ignoreOrder: true);
    }

    [Fact]
    public async Task ListEvents_ReturnsTheExternalUrlWithoutOpeningTheEvent_WhenTheEventHasOne()
    {
        var (from, to) = Window(2089);
        var id = Guid.NewGuid();
        await SeedEventAsync(
            id,
            EventType.Roadshow,
            EventStatus.Planning,
            new DateOnly(2089, 3, 1),
            externalUrl: "https://sorteo360.local/roadshow-2089");

        var body = await GetListAsync($"v1/events?startDateFrom={from}&startDateTo={to}");

        body.Items.Single(e => e.Id == id).ExternalUrl.ShouldBe("https://sorteo360.local/roadshow-2089");
    }

    [Fact]
    public async Task ListEvents_ReturnsExternalUrlAsNull_WhenTheEventHasNone()
    {
        // Nula, no cadena vacía: quien lee el listado decide con el campo, y un "" haría pasar
        // por lanzado un evento que no lo está.
        var (from, to) = Window(2091);
        var id = Guid.NewGuid();
        await SeedEventAsync(id, EventType.Roadshow, EventStatus.Planning, new DateOnly(2091, 3, 1));

        var response = await _client.GetAsync($"v1/events?startDateFrom={from}&startDateTo={to}");
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, content);

        using var json = JsonDocument.Parse(content);
        var item = json.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == id);

        item.GetProperty("externalUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("v1/events?status=noexiste")]
    [InlineData("v1/events?active=true&status=closed")]
    [InlineData("v1/events?pageSize=0")]
    [InlineData("v1/events?pageSize=101")]
    [InlineData("v1/events?page=0")]
    [InlineData("v1/events?startDateFrom=2090-12-31&startDateTo=2090-01-01")]
    public async Task ListEvents_Returns400_WhenQueryIsInvalid(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helpers

    private static CreateEventRequest CreateValidRequest() =>
        new()
        {
            Type      = EventType.Roadshow,
            Name      = "Test Roadshow 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate   = new DateOnly(2026, 9, 5),
            City      = "Madrid",
            Country   = "Spain"
        };

    /// <summary>
    /// Ventana de fechas (cadenas ISO) que abarca un año completo. Cada test de listado usa
    /// un año propio para aislar su conjunto sembrado del resto de la tabla compartida.
    /// </summary>
    private static (string From, string To) Window(int year) =>
        ($"{year}-01-01", $"{year}-12-31");

    private async Task<PagedResponse<EventListItemResponse>> GetListAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, content);

        var body = JsonSerializer.Deserialize<PagedResponse<EventListItemResponse>>(
            content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.ShouldNotBeNull();
        return body!;
    }

    /// <summary>
    /// Siembra un evento directamente vía el DbContext (la única forma de fijar status y fechas
    /// arbitrarias: el alta por la API siempre crea en estado 'planning').
    /// </summary>
    private async Task SeedEventAsync(
        Guid id,
        string type,
        string status,
        DateOnly startDate,
        DateTimeOffset? createdAt = null,
        string? externalUrl = null)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var now = createdAt ?? DateTimeOffset.UtcNow;

        context.Events.Add(new EventData
        {
            Id          = id,
            Type        = type,
            Name        = "Seed Evento",
            StartDate   = startDate,
            EndDate     = startDate.AddDays(1),
            Status      = status,
            Language    = AllowedLanguage.Espanol,
            Visibility  = EventVisibility.Private,
            ExternalUrl = externalUrl,
            CreatedAt   = now,
            UpdatedAt   = now,
            CreatedById = Guid.NewGuid(),
            UpdatedById = Guid.NewGuid()
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Siembra un evento de cada uno de los cuatro estados dentro de la ventana de un año.
    /// </summary>
    private async Task SeedWindowWithAllStatusesAsync(int year)
    {
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Planning, new DateOnly(year, 3, 1));
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.InProgress, new DateOnly(year, 6, 1));
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Closed, new DateOnly(year, 9, 1));
        await SeedEventAsync(Guid.NewGuid(), EventType.Roadshow, EventStatus.Cancelled, new DateOnly(year, 12, 1));
    }

    #endregion
}

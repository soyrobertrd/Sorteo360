using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

/// <summary>
/// La búsqueda, el orden y el filtro por OPT del listado de eventos, contra la base de verdad:
/// el orden por costo total y el <c>ILIKE</c> de la búsqueda los decide PostgreSQL, así que un
/// doble en memoria diría que funcionan sin haberlos ejercitado.
/// <para>
/// Cada test siembra en su propia ventana de un año y la manda en el query, igual que el resto
/// de los tests de listado: la tabla es compartida por toda la clase.
/// </para>
/// </summary>
public sealed class EventListQueryTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly EventsApiDbFactory _factory;

    public EventListQueryTests(EventsApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region searchTerm

    [Fact]
    public async Task Search_MatchesTheNamePartiallyAndIgnoringCase_WhenATermIsGiven()
    {
        var (from, to) = Window(2101);
        await SeedAsync(2101, "Feria Internacional de MADRID");
        await SeedAsync(2101, "Roadshow madrid 2101");
        await SeedAsync(2101, "Webinar Santiago");

        var body = await GetListAsync($"v1/events?searchTerm=madrid&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(2);
        body.Items.ShouldAllBe(e => e.Name.Contains("adrid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_TreatsWildcardsAsLiteralCharacters_WhenTheTermHasThem()
    {
        var (from, to) = Window(2103);
        await SeedAsync(2103, "Cumbre 100% digital");
        await SeedAsync(2103, "Cumbre presencial");

        var body = await GetListAsync($"v1/events?searchTerm=%25&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(1);
        body.Items.Single().Name.ShouldBe("Cumbre 100% digital");
    }

    [Fact]
    public async Task Search_IsIgnored_WhenTheTermIsEmpty()
    {
        var (from, to) = Window(2104);
        await SeedAsync(2104, "Alfa");
        await SeedAsync(2104, "Bravo");

        var body = await GetListAsync($"v1/events?searchTerm=&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(2);
    }

    [Fact]
    public async Task Search_NarrowsTheTotalAndNotJustThePage_WhenATermIsGiven()
    {
        var (from, to) = Window(2110);
        await SeedAsync(2110, "Gira norte 1");
        await SeedAsync(2110, "Gira norte 2");
        await SeedAsync(2110, "Gira norte 3");
        await SeedAsync(2110, "Congreso");
        await SeedAsync(2110, "Webinar");

        var body = await GetListAsync($"v1/events?searchTerm=gira&pageSize=2&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(3);
        body.Items.Count.ShouldBe(2);
    }

    #endregion

    #region sortField / sortOrder

    [Fact]
    public async Task SortByName_OrdersAscendingAndDescending_WhenEitherOrderIsAsked()
    {
        var (from, to) = Window(2105);
        await SeedAsync(2105, "Charlie");
        await SeedAsync(2105, "Alfa");
        await SeedAsync(2105, "Bravo");

        var ascending = await GetListAsync($"v1/events?sortField=name&sortOrder=asc&startDateFrom={from}&startDateTo={to}");
        var descending = await GetListAsync($"v1/events?sortField=name&sortOrder=desc&startDateFrom={from}&startDateTo={to}");

        ascending.Items.Select(e => e.Name).ShouldBe(["Alfa", "Bravo", "Charlie"]);
        descending.Items.Select(e => e.Name).ShouldBe(["Charlie", "Bravo", "Alfa"]);
    }

    [Fact]
    public async Task SortByStatus_FollowsTheLifecycleAndNotTheAlphabet_WhenEventsSpanSeveralStatuses()
    {
        var (from, to) = Window(2106);
        await SeedAsync(2106, "Cerrado", status: EventStatus.Closed);
        await SeedAsync(2106, "Planificando", status: EventStatus.Planning);
        await SeedAsync(2106, "Cancelado", status: EventStatus.Cancelled);
        await SeedAsync(2106, "En curso", status: EventStatus.InProgress);

        var body = await GetListAsync($"v1/events?sortField=status&sortOrder=asc&startDateFrom={from}&startDateTo={to}");

        body.Items.Select(e => e.Status).ShouldBe(
            [EventStatus.Planning, EventStatus.InProgress, EventStatus.Closed, EventStatus.Cancelled]);
    }

    [Fact]
    public async Task SortByStartDate_OrdersAscending_WhenAscIsAsked()
    {
        var (from, to) = Window(2111);
        await SeedAsync(2111, "Tercero", startDate: new DateOnly(2111, 9, 1));
        await SeedAsync(2111, "Primero", startDate: new DateOnly(2111, 1, 1));
        await SeedAsync(2111, "Segundo", startDate: new DateOnly(2111, 5, 1));

        var body = await GetListAsync($"v1/events?sortField=startDate&sortOrder=asc&startDateFrom={from}&startDateTo={to}");

        body.Items.Select(e => e.Name).ShouldBe(["Primero", "Segundo", "Tercero"]);
    }

    [Fact]
    public async Task SortByTotalCost_GroupsByCurrencyWithUsdFirstThenByAmount_WhenAscending()
    {
        var (from, to) = Window(2107);
        await SeedTotalCostWindowAsync(2107);

        var body = await GetListAsync($"v1/events?sortField=totalCost&sortOrder=asc&startDateFrom={from}&startDateTo={to}");

        // Ordenar por el número pelado pondría EUR 50 antes que USD 100 y EUR 200 antes que
        // USD 300, que es exactamente la mentira que evita agrupar por moneda.
        body.Items.Select(e => e.Name).ShouldBe(["USD 100", "USD 300", "EUR 50", "EUR 200", "Sin costo"]);
    }

    [Fact]
    public async Task SortByTotalCost_KeepsUsdFirstAndReversesOnlyTheAmount_WhenDescending()
    {
        var (from, to) = Window(2112);
        await SeedTotalCostWindowAsync(2112);

        var body = await GetListAsync($"v1/events?sortField=totalCost&sortOrder=desc&startDateFrom={from}&startDateTo={to}");

        // La moneda es el agrupamiento, no una cifra: invertirla pondría EUR delante y dejaría de
        // cumplirse "USD primero", que no depende del sentido.
        body.Items.Select(e => e.Name).ShouldBe(["USD 300", "USD 100", "EUR 200", "EUR 50", "Sin costo"]);
    }

    [Fact]
    public async Task SortByTotalCost_LeavesEventsWithoutTotalCostLast_WhenSortingInEitherDirection()
    {
        var (from, to) = Window(2113);
        await SeedAsync(2113, "Sin costo A");
        await SeedAsync(2113, "Con costo", totalCost: new Money(10m, Currency.USD));
        await SeedAsync(2113, "Sin costo B");

        var ascending = await GetListAsync($"v1/events?sortField=totalCost&sortOrder=asc&startDateFrom={from}&startDateTo={to}");
        var descending = await GetListAsync($"v1/events?sortField=totalCost&sortOrder=desc&startDateFrom={from}&startDateTo={to}");

        ascending.Items[0].Name.ShouldBe("Con costo");
        ascending.Items.Skip(1).Select(e => e.Name).ShouldBe(["Sin costo A", "Sin costo B"], ignoreOrder: true);
        descending.Items[0].Name.ShouldBe("Con costo");
        descending.Items.Skip(1).Select(e => e.Name).ShouldBe(["Sin costo A", "Sin costo B"], ignoreOrder: true);
    }

    [Fact]
    public async Task Sort_KeepsTheStartDateDescendingDefault_WhenNoFieldIsGiven()
    {
        var (from, to) = Window(2114);
        await SeedAsync(2114, "Primero", startDate: new DateOnly(2114, 1, 1));
        await SeedAsync(2114, "Tercero", startDate: new DateOnly(2114, 9, 1));
        await SeedAsync(2114, "Segundo", startDate: new DateOnly(2114, 5, 1));

        var body = await GetListAsync($"v1/events?startDateFrom={from}&startDateTo={to}");

        body.Items.Select(e => e.Name).ShouldBe(["Tercero", "Segundo", "Primero"]);
    }

    [Fact]
    public async Task SortField_MatchesIgnoringCase_WhenCasingDiffers()
    {
        var (from, to) = Window(2115);
        await SeedAsync(2115, "Bravo");
        await SeedAsync(2115, "Alfa");

        var body = await GetListAsync($"v1/events?sortField=NAME&sortOrder=ASC&startDateFrom={from}&startDateTo={to}");

        body.Items.Select(e => e.Name).ShouldBe(["Alfa", "Bravo"]);
    }

    [Fact]
    public async Task SortField_Returns400WithItsOwnCode_WhenOutsideTheClosedSet()
    {
        var (code, status) = await GetErrorCodeAsync("v1/events?sortField=estimatedBudget");

        status.ShouldBe(HttpStatusCode.BadRequest);
        code.ShouldBe("EVENT_SORT_FIELD_NOT_SUPPORTED");
    }

    [Fact]
    public async Task SortField_IsRejectedLikeAnyOtherUnknownField_WhenItIsANumber()
    {
        // Enum.TryParse aceptaría "0" como el primer valor del enum. Un número no es un campo.
        var (code, status) = await GetErrorCodeAsync("v1/events?sortField=0");

        status.ShouldBe(HttpStatusCode.BadRequest);
        code.ShouldBe("EVENT_SORT_FIELD_NOT_SUPPORTED");
    }

    [Fact]
    public async Task SortOrder_Returns400WithItsOwnCode_WhenOutsideAscOrDesc()
    {
        var (code, status) = await GetErrorCodeAsync("v1/events?sortField=name&sortOrder=sideways");

        status.ShouldBe(HttpStatusCode.BadRequest);
        code.ShouldBe("EVENT_SORT_ORDER_NOT_SUPPORTED");
    }

    #endregion

    #region Combinados

    [Fact]
    public async Task SearchAndSort_CombineWithTheOlderFilters_WhenAllAreGiven()
    {
        var (from, to) = Window(2109);
        await SeedAsync(2109, "Gira sur alta", status: EventStatus.Planning,
            type: EventType.Roadshow, totalCost: new Money(900m, Currency.USD));
        await SeedAsync(2109, "Gira sur baja", status: EventStatus.Planning,
            type: EventType.Roadshow, totalCost: new Money(100m, Currency.USD));
        // Cada uno cae por un filtro distinto: el nombre, el estado y el tipo.
        await SeedAsync(2109, "Congreso norte", status: EventStatus.Planning,
            type: EventType.Roadshow);
        await SeedAsync(2109, "Gira sur cerrada", status: EventStatus.Closed,
            type: EventType.Roadshow);
        await SeedAsync(2109, "Gira sur feria", status: EventStatus.Planning,
            type: EventType.Christmas);

        var body = await GetListAsync(
            $"v1/events?searchTerm=gira+sur&status=planning&type=Corporate" +
            $"&sortField=totalCost&sortOrder=desc&startDateFrom={from}&startDateTo={to}");

        body.Total.ShouldBe(2);
        body.Items.Select(e => e.Name).ShouldBe(["Gira sur alta", "Gira sur baja"]);
    }

    #endregion

    #region Helpers

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

    /// <summary>El <c>code</c> de la extensión de ProblemDetails, junto con el estado devuelto.</summary>
    private async Task<(string? Code, HttpStatusCode Status)> GetErrorCodeAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(content);
        var code = json.RootElement.TryGetProperty("code", out var value) ? value.GetString() : null;

        return (code, response.StatusCode);
    }

    /// <summary>
    /// Los cinco eventos del orden por costo total: dos de cada moneda y uno sin costo, sembrados
    /// en desorden para que el orden que se afirma solo pueda venir de la consulta.
    /// </summary>
    private async Task SeedTotalCostWindowAsync(int year)
    {
        await SeedAsync(year, "EUR 200", totalCost: new Money(200m, Currency.EUR));
        await SeedAsync(year, "USD 300", totalCost: new Money(300m, Currency.USD));
        await SeedAsync(year, "Sin costo");
        await SeedAsync(year, "EUR 50", totalCost: new Money(50m, Currency.EUR));
        await SeedAsync(year, "USD 100", totalCost: new Money(100m, Currency.USD));
    }

    /// <summary>
    /// Siembra un evento vía el DbContext: es la única forma de fijar estado, costo total y
    /// oficina arbitrarios, porque el alta por la API siempre crea en 'planning' y sin costo.
    /// </summary>
    private async Task SeedAsync(
        int year,
        string name,
        string? status = null,
        string? type = null,
        DateOnly? startDate = null,
        Money? totalCost = null,
        string? visibility = null)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var start = startDate ?? new DateOnly(year, 6, 1);

        context.Events.Add(new EventData
        {
            Id          = Guid.NewGuid(),
            Type        = type ?? EventType.Roadshow,
            Name        = name,
            StartDate   = start,
            EndDate     = start.AddDays(1),
            Status      = status ?? EventStatus.Planning,
            Language    = AllowedLanguage.Espanol,
            Visibility  = visibility ?? EventVisibility.Private,
            TotalCost   = totalCost,
            CreatedAt   = now,
            UpdatedAt   = now,
            CreatedById = Guid.NewGuid(),
            UpdatedById = Guid.NewGuid()
        });

        await context.SaveChangesAsync();
    }

    #endregion
}

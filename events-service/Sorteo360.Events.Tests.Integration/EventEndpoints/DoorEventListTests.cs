using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

/// <summary>
/// La lista de eventos de la puerta (spec <c>convocatorias/10</c> §4): los eventos que pasan alrededor
/// de hoy, para que una terminal elija uno. La ventana y el orden los decide PostgreSQL, así que se
/// prueban contra la base de verdad.
/// <para>
/// La tabla es compartida por toda la clase: cada test fija su propio hoy en un año aparte y siembra
/// solo alrededor de él, así que ningún test ve los eventos de otro.
/// </para>
/// </summary>
public sealed class DoorEventListTests : IClassFixture<EventsApiDbFactory>
{
    private const string Route = "v1/events/door";

    private readonly HttpClient _client;
    private readonly EventsApiDbFactory _factory;

    public DoorEventListTests(EventsApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListDoorEvents_ReturnsEventsOneDayEitherSideOfToday_WhenTheyTouchTheWindow()
    {
        var today = new DateOnly(2401, 6, 15);
        _factory.Clock.Pin(today);
        var startsTomorrow = await SeedAsync("Empieza mañana", today.AddDays(1), today.AddDays(3));
        var endedYesterday = await SeedAsync("Terminó ayer", today.AddDays(-3), today.AddDays(-1));

        var ids = await GetIdsAsync();

        ids.ShouldBe([endedYesterday, startsTomorrow], ignoreOrder: true);
    }

    [Fact]
    public async Task ListDoorEvents_LeavesOutEventsTwoDaysAway_WhenTheyMissTheWindow()
    {
        var today = new DateOnly(2402, 6, 15);
        _factory.Clock.Pin(today);
        await SeedAsync("Terminó anteayer", today.AddDays(-4), today.AddDays(-2));
        await SeedAsync("Empieza pasado mañana", today.AddDays(2), today.AddDays(4));

        var ids = await GetIdsAsync();

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListDoorEvents_ReturnsAnEventSpanningTheWindow_WhenItStartedBeforeAndEndsAfter()
    {
        var today = new DateOnly(2403, 6, 15);
        _factory.Clock.Pin(today);
        var spanning = await SeedAsync("Feria de una semana", today.AddDays(-5), today.AddDays(5));

        var ids = await GetIdsAsync();

        ids.ShouldBe([spanning]);
    }

    [Fact]
    public async Task ListDoorEvents_LeavesOutACancelledEvent_WhenItIsInsideTheWindow()
    {
        var today = new DateOnly(2404, 6, 15);
        _factory.Clock.Pin(today);
        await SeedAsync("Cancelado", today, today, status: EventStatus.Cancelled);
        var inProgress = await SeedAsync("En curso", today, today, status: EventStatus.InProgress);
        var closed = await SeedAsync("Cerrado", today, today, status: EventStatus.Closed);

        var ids = await GetIdsAsync();

        ids.ShouldBe([inProgress, closed], ignoreOrder: true);
    }

    [Fact]
    public async Task ListDoorEvents_ReturnsPrivateAndPublicEvents_WhenBothAreInsideTheWindow()
    {
        var today = new DateOnly(2405, 6, 15);
        _factory.Clock.Pin(today);
        var privateEvent = await SeedAsync("Privado", today, today, visibility: EventVisibility.Private);
        var publicEvent = await SeedAsync("Público", today, today, visibility: EventVisibility.Public);

        var ids = await GetIdsAsync();

        ids.ShouldBe([privateEvent, publicEvent], ignoreOrder: true);
    }

    [Fact]
    public async Task ListDoorEvents_SortsByStartDateThenName_WhenSeveralAreInsideTheWindow()
    {
        var today = new DateOnly(2406, 6, 15);
        _factory.Clock.Pin(today);
        var lateB = await SeedAsync("B mañana", today.AddDays(1), today.AddDays(1));
        var earlyB = await SeedAsync("B ayer", today.AddDays(-1), today.AddDays(1));
        var lateA = await SeedAsync("A mañana", today.AddDays(1), today.AddDays(1));
        var earlyA = await SeedAsync("A ayer", today.AddDays(-1), today.AddDays(1));

        var ids = await GetIdsAsync();

        ids.ShouldBe([earlyA, earlyB, lateA, lateB]);
    }

    [Fact]
    public async Task ListDoorEvents_ReturnsOnlyThePickerFields_WhenAnEventIsInsideTheWindow()
    {
        var today = new DateOnly(2407, 6, 15);
        _factory.Clock.Pin(today);
        var id = await SeedAsync("Feria de Turismo de Punta Cana", today, today.AddDays(1));

        using var json = await GetJsonAsync();

        var item = json.RootElement.EnumerateArray().ShouldHaveSingleItem();
        item.EnumerateObject().Select(p => p.Name)
            .ShouldBe(["id", "name", "startDate", "endDate", "cities"], ignoreOrder: true);
        item.GetProperty("id").GetGuid().ShouldBe(id);
        item.GetProperty("name").GetString().ShouldBe("Feria de Turismo de Punta Cana");
        item.GetProperty("startDate").GetString().ShouldBe("2407-06-15");
        item.GetProperty("endDate").GetString().ShouldBe("2407-06-16");
    }

    [Fact]
    public async Task ListDoorEvents_ReturnsTheLiveCityAlone_WhenTheEventHasARemovedOne()
    {
        var today = new DateOnly(2408, 6, 15);
        _factory.Clock.Pin(today);
        var id = await SeedAsync("Roadshow", today, today);
        await SeedCityAsync(id, "Quito", displayOrder: 1, deleted: true);
        await SeedCityAsync(id, "Lima", displayOrder: 1);

        using var json = await GetJsonAsync();

        var cities = json.RootElement.EnumerateArray().ShouldHaveSingleItem().GetProperty("cities");
        cities.EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .ShouldBe(["Lima"]);
    }

    [Fact]
    public async Task ListDoorEvents_LeavesOutASoftDeletedEvent_WhenItIsInsideTheWindow()
    {
        var today = new DateOnly(2409, 6, 15);
        _factory.Clock.Pin(today);
        await SeedAsync("Borrado", today, today, deleted: true);

        var ids = await GetIdsAsync();

        ids.ShouldBeEmpty();
    }

    /// <summary>
    /// La terminal entra con el token de la puerta, que no lleva <c>platform:staff</c>: el
    /// <c>check-in:read</c> que trae basta (spec <c>convocatorias/10</c>, definición de terminado).
    /// </summary>
    [Theory]
    [InlineData(TestAuthHandler.DoorStaffRoles)]
    [InlineData(AuthorizationPolicies.CheckInRead)]
    public async Task ListDoorEvents_Returns200_WhenTheCallerHoldsOnlyTheDoorRoles(string roles)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add(TestAuthHandler.RolesHeader, roles);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<IReadOnlyList<Guid>> GetIdsAsync()
    {
        using var json = await GetJsonAsync();
        return json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
    }

    private async Task<JsonDocument> GetJsonAsync()
    {
        var response = await _client.GetAsync(Route);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, content);

        return JsonDocument.Parse(content);
    }

    private async Task<Guid> SeedAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string? status = null,
        string? visibility = null,
        bool deleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();

        context.Events.Add(new EventData
        {
            Id          = id,
            Type        = EventType.Roadshow,
            Name        = name,
            StartDate   = startDate,
            EndDate     = endDate,
            Status      = status ?? EventStatus.Planning,
            Language    = AllowedLanguage.Espanol,
            Visibility  = visibility ?? EventVisibility.Private,
            CreatedAt   = now,
            UpdatedAt   = now,
            CreatedById = Guid.NewGuid(),
            UpdatedById = Guid.NewGuid(),
            DeletedAt   = deleted ? now : null
        });

        await context.SaveChangesAsync();
        return id;
    }

    private async Task SeedCityAsync(Guid eventId, string name, int displayOrder, bool deleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var now = DateTimeOffset.UtcNow;

        context.RoadshowCities.Add(new RoadshowCityData
        {
            Id           = Guid.NewGuid(),
            EventId      = eventId,
            Name         = name,
            Country      = "PE",
            DisplayOrder = displayOrder,
            CreatedAt    = now,
            UpdatedAt    = now,
            CreatedById  = Guid.NewGuid(),
            UpdatedById  = Guid.NewGuid(),
            DeletedAt    = deleted ? now : null
        });

        await context.SaveChangesAsync();
    }
}

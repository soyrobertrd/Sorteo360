using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.EventEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Auth;

/// <summary>
/// Verifica que las escrituras estampan los ids de auditoría con el usuario del
/// token (claim <c>sub</c>) — el <c>Guid.Empty</c> desaparece — y que las
/// lecturas solo exigen autenticación, sin rol funcional alguno.
/// </summary>
public sealed class ActorStampingTests : IClassFixture<EventsApiDbFactory>
{
    private readonly EventsApiDbFactory _factory;

    public ActorStampingTests(EventsApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateEvent_StampsCreatedByIdWithTheTokenSub_WhenTheCallerIsAuthenticated()
    {
        var actorId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, actorId.ToString());

        var response = await client.PostAsJsonAsync("v1/events", CreateValidRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var data = await dbContext.Set<EventData>().IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == id);

        data.CreatedById.ShouldBe(actorId);
        data.CreatedById.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task ListEvents_RequiresNoFunctionalRole_WhenTheCallerHoldsPlatformStaff()
    {
        using var client = _factory.CreateClient();
        // Personal sin ningún rol funcional: las lecturas deben responder (ADR-0025).
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.PlatformStaff);

        var response = await client.GetAsync("v1/events");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static CreateEventRequest CreateValidRequest() =>
        new()
        {
            Type      = EventType.Christmas,
            Name      = $"Evento Auditoría {Guid.NewGuid():N}",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate   = new DateOnly(2026, 9, 5),
            City      = "Santo Domingo",
            Country   = "Dominican Republic",
        };
}

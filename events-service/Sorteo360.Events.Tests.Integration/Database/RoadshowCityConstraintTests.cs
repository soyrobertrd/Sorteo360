using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Npgsql;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Database;

/// <summary>
/// "Un evento tiene exactamente una ciudad" lo dice el índice único de <c>roadshow_city</c>, probado
/// con SQL directo porque ninguna ruta llega a escribir una segunda. El índice mira solo las ciudades
/// vivas: una dada de baja no le impide al evento tener la suya.
/// </summary>
public sealed class RoadshowCityConstraintTests : IClassFixture<EventsApiDbFactory>
{
    private readonly EventsApiDbFactory _factory;

    public RoadshowCityConstraintTests(EventsApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Insert_IsRejected_WhenTheEventAlreadyHasALiveCity()
    {
        var eventId = await InsertEventAsync();
        await InsertCityAsync(eventId);

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertCityAsync(eventId));

        exception.ConstraintName.ShouldBe("ux_roadshow_city_event_id");
    }

    [Fact]
    public async Task Insert_IsAccepted_WhenTheEventsOtherCityWasRemoved()
    {
        var eventId = await InsertEventAsync();
        await InsertCityAsync(eventId, deletedAt: DateTimeOffset.UtcNow);

        await Should.NotThrowAsync(() => InsertCityAsync(eventId));
    }

    private async Task<Guid> InsertEventAsync()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.event
                (id, type, name, start_date, end_date, status, visibility, language,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({eventId}, {EventType.Roadshow}, 'Roadshow de una ciudad', {new DateOnly(2026, 10, 1)},
                 {new DateOnly(2026, 10, 5)}, {EventStatus.Planning}, 'private', 'es',
                 now(), now(), {actorId}, {actorId})
            """);

        return eventId;
    }

    private async Task InsertCityAsync(Guid eventId, DateTimeOffset? deletedAt = null)
    {
        var actorId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.roadshow_city
                (id, event_id, name, country, time_zone, display_order,
                 created_at, updated_at, created_by_id, updated_by_id, deleted_at)
            VALUES
                ({Guid.NewGuid()}, {eventId}, 'Santo Domingo', 'Dominican Republic', 'America/Santo_Domingo', 1,
                 now(), now(), {actorId}, {actorId}, {deletedAt})
            """);
    }
}

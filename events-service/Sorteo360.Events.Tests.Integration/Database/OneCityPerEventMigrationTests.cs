using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Sorteo360.Events.Tests.Integration.Database;

/// <summary>
/// La migración que deja una sola ciudad viva por evento conserva la primera y da de baja las demás
/// antes de crear el índice único.
/// </summary>
/// <remarks>
/// Un Roadshow armado con la ruta de agregar ciudades, ya retirada, puede traer varias. "La
/// primera" es la que el acta notarial ya imprimía: la de menor <c>display_order</c> y, a igual
/// orden, la que se creó antes. Así ninguna acta emitida cambia de lugar. Sin este relleno el
/// índice no se crearía, y como las migraciones corren al arrancar, el servicio no levantaría.
/// </remarks>
public sealed class OneCityPerEventMigrationTests : IAsyncLifetime
{
    /// <summary>La última migración antes de la que exige una ciudad por evento.</summary>
    private const string PreviousMigration = "20261102011452_RaffleCertificateSigners";

    private static readonly DateTimeOffset Earlier = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid RoadshowId = Guid.NewGuid();
    private static readonly Guid FirstCreatedSecondShown = Guid.NewGuid();
    private static readonly Guid FirstShown = Guid.NewGuid();
    private static readonly Guid TiedButCreatedLater = Guid.NewGuid();
    private static readonly Guid AlreadyRemoved = Guid.NewGuid();

    private static readonly Guid FeriaId = Guid.NewGuid();
    private static readonly Guid FeriaCity = Guid.NewGuid();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("events")
        .Build();

    private EventsDbContext _context = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        _context = new EventsDbContext(new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

        await _context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        await SeedEventAsync(RoadshowId, "Roadshow");
        await SeedCityAsync(FirstCreatedSecondShown, RoadshowId, displayOrder: 2, Earlier);
        await SeedCityAsync(FirstShown, RoadshowId, displayOrder: 1, Earlier.AddDays(1));
        await SeedCityAsync(TiedButCreatedLater, RoadshowId, displayOrder: 1, Earlier.AddDays(2));
        await SeedCityAsync(AlreadyRemoved, RoadshowId, displayOrder: 0, Earlier, deletedAt: Earlier);

        await SeedEventAsync(FeriaId, "Feria");
        await SeedCityAsync(FeriaCity, FeriaId, displayOrder: 1, Earlier);

        await _context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Migrate_KeepsTheCityTheNotarialRecordPrinted_AndRemovesTheRest()
    {
        var live = await LiveCityIdsAsync(RoadshowId);

        live.ShouldBe([FirstShown]);
    }

    [Fact]
    public async Task Migrate_StampsTheRemovedCitiesAsDeleted_AndLeavesAnEarlierRemovalAlone()
    {
        var cities = await _context.RoadshowCities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.EventId == RoadshowId)
            .ToDictionaryAsync(c => c.Id, c => c.DeletedAt);

        cities[FirstCreatedSecondShown].ShouldNotBeNull();
        cities[TiedButCreatedLater].ShouldNotBeNull();
        cities[AlreadyRemoved].ShouldBe(Earlier);
    }

    [Fact]
    public async Task Migrate_LeavesAnEventWithOneCityAsItWas()
    {
        var live = await LiveCityIdsAsync(FeriaId);

        live.ShouldBe([FeriaCity]);
    }

    private Task<List<Guid>> LiveCityIdsAsync(Guid eventId) =>
        _context.RoadshowCities
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .Select(c => c.Id)
            .ToListAsync();

    private async Task SeedEventAsync(Guid eventId, string type)
    {
        var actorId = Guid.NewGuid();

        await _context.Database.ExecuteSqlAsync($"""
            INSERT INTO events.event
                (id, type, name, start_date, end_date, opt_office_id, status, visibility, language,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({eventId}, {type}, 'Evento Migración', {new DateOnly(2026, 9, 1)},
                 {new DateOnly(2026, 9, 5)}, {Guid.NewGuid()}, {EventStatus.Planning}, 'private', 'es',
                 now(), now(), {actorId}, {actorId})
            """);
    }

    private async Task SeedCityAsync(
        Guid cityId, Guid eventId, int displayOrder, DateTimeOffset createdAt, DateTimeOffset? deletedAt = null)
    {
        var actorId = Guid.NewGuid();

        await _context.Database.ExecuteSqlAsync($"""
            INSERT INTO events.roadshow_city
                (id, event_id, name, country, time_zone, display_order,
                 created_at, updated_at, created_by_id, updated_by_id, deleted_at)
            VALUES
                ({cityId}, {eventId}, 'Ciudad', 'Dominican Republic', 'America/Santo_Domingo', {displayOrder},
                 {createdAt}, {createdAt}, {actorId}, {actorId}, {deletedAt})
            """);
    }
}

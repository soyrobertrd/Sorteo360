using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Sorteo360.Events.Tests.Integration.Database;

/// <summary>
/// La migración que agrega la zona horaria, no nula, le da la de Santo Domingo a cada ciudad que ya
/// existía.
/// </summary>
/// <remarks>
/// Santo Domingo es la hora que imprimió toda acta emitida antes de este campo, así que una
/// reemisión de un acta vieja sigue diciendo las mismas horas. El resto de los tests de integración
/// parte de una base vacía, donde el relleno no encuentra filas; este siembra una ciudad con el
/// esquema anterior y solo entonces aplica la migración.
/// </remarks>
public sealed class RoadshowCityTimeZoneBackfillTests : IAsyncLifetime
{
    /// <summary>La última migración antes de la que agrega la zona horaria.</summary>
    private const string PreviousMigration = "20261027211325_AddPrizeSponsorLogo";

    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CityId = Guid.NewGuid();

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
        await SeedCityAsync();
        await _context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Migrate_GivesAnExistingCitySantoDomingoTime()
    {
        var city = await _context.RoadshowCities.AsNoTracking().SingleAsync(c => c.Id == CityId);

        city.TimeZone.ShouldBe("America/Santo_Domingo");
    }

    private async Task SeedCityAsync()
    {
        var actorId = Guid.NewGuid();

        await _context.Database.ExecuteSqlAsync($"""
            INSERT INTO events.event
                (id, type, name, start_date, end_date, opt_office_id, status, visibility, language,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({EventId}, 'Roadshow', 'Roadshow Migración', {new DateOnly(2026, 9, 1)},
                 {new DateOnly(2026, 9, 5)}, {Guid.NewGuid()}, {EventStatus.Planning}, 'private', 'es',
                 now(), now(), {actorId}, {actorId})
            """);

        await _context.Database.ExecuteSqlAsync($"""
            INSERT INTO events.roadshow_city
                (id, event_id, name, country, display_order,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({CityId}, {EventId}, 'Santo Domingo', 'Dominican Republic', 1,
                 now(), now(), {actorId}, {actorId})
            """);
    }
}

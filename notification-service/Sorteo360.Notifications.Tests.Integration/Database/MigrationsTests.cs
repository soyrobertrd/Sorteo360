using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Notifications.Infrastructure.Database;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Database;

/// <summary>
/// Las migraciones de EF Core son la única autoridad del esquema, y esta suite las corre
/// contra un PostgreSQL real, el mismo DDL que aplica el arranque en cada entorno.
/// <c>infra/postgres/init.sh</c> solo crea la base vacía.
/// </summary>
public sealed class MigrationsTests : IClassFixture<NotificationsApiDbFactory>
{
    private readonly NotificationsApiDbFactory _factory;

    public MigrationsTests(NotificationsApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task Migrate_ReturnsNoPendingMigrations_WhenAppliedToAnEmptyDatabase()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var pending = await context.Database.GetPendingMigrationsAsync();
        var applied = await context.Database.GetAppliedMigrationsAsync();

        pending.ShouldBeEmpty();
        applied.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Migrate_ReturnsTheNotificationsSchema_WhenApplied()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'notifications'";

        var schemas = Convert.ToInt32(await command.ExecuteScalarAsync());

        schemas.ShouldBe(1);
    }
}

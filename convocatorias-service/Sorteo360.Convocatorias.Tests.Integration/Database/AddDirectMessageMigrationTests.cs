using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Database;

/// <summary>
/// <c>AddDirectMessage</c> sobre una base que ya tiene campañas de evento y no tiene la plantilla
/// nueva: la de stage y la de producción el día que se despliegue. Va en su propia clase, con su
/// propio contenedor, porque deshace el esquema mientras corre y ningún otro test puede estar
/// usándolo a la vez.
/// </summary>
public sealed class AddDirectMessageMigrationTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private const string PreviousMigration = "20261003044246_AddCheckIn";

    private readonly ConvocatoriasApiDbFactory _factory;

    public AddDirectMessageMigrationTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task AddDirectMessage_ReturnsTheExistingCampaignIntactAndTheTemplateSeeded_WhenAppliedOverRows()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        var migrator = context.GetService<IMigrator>();

        // De bajada, la plantilla se va con la migración: es la base de antes, sin ella.
        await migrator.MigrateAsync(PreviousMigration);
        (await CountDirectMessageTemplatesAsync(context)).ShouldBe(0);

        var campaignId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO convocatorias.campaign
                 (id, event_id, template_key, language, state, created_at, updated_at, created_by_id, updated_by_id)
             VALUES
                 ({campaignId}, {eventId}, 'registration_invite', 'es', 'draft', now(), now(), {Guid.Empty}, {Guid.Empty})
             """);

        await migrator.MigrateAsync();

        (await context.Database.SqlQuery<Guid?>(
                $"""SELECT event_id AS "Value" FROM convocatorias.campaign WHERE id = {campaignId}""")
            .SingleAsync()).ShouldBe(eventId);

        (await context.Database.SqlQuery<int>(
                $"""
                 SELECT COUNT(*)::int AS "Value" FROM convocatorias.campaign
                 WHERE id = {campaignId} AND subject IS NULL AND message IS NULL
                 """)
            .SingleAsync()).ShouldBe(1);

        (await context.Database.SqlQuery<string>(
                $"""SELECT body_html AS "Value" FROM convocatorias.template WHERE template_key = 'direct_message'""")
            .SingleAsync()).ShouldBe(TemplateSeed.Rows.Single(t => t.TemplateKey == TemplateKey.DirectMessage).BodyHtml);

        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    private static Task<int> CountDirectMessageTemplatesAsync(ConvocatoriasDbContext context) =>
        context.Database.SqlQuery<int>(
            $"""SELECT COUNT(*)::int AS "Value" FROM convocatorias.template WHERE template_key = 'direct_message'""")
            .SingleAsync();
}

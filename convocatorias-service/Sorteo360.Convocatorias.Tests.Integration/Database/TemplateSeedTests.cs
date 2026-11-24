using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Database;

/// <summary>
/// La siembra de <c>template</c> contra un PostgreSQL real, aplicada por las mismas migraciones
/// que corre el arranque en cada entorno.
/// <para>
/// Que la siembra sea <c>InsertData</c> y no <c>HasData</c> no se prueba acá sino en
/// <c>Tests.Unit/Database/TemplateSeedIsNotModelManagedTests</c>: es una propiedad del modelo, y
/// volver a correr <c>MigrateAsync</c> sin migraciones pendientes no hace nada bajo ninguna de
/// las dos, así que probarlo por este lado no diría nada.
/// </para>
/// </summary>
public sealed class TemplateSeedTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public TemplateSeedTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    private async Task<T> WithContextAsync<T>(Func<ConvocatoriasDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>());
    }

    [Fact]
    public async Task Seed_ReturnsTheFiveSeededRows_WhenApplied()
    {
        var keys = await WithContextAsync(context => context.Database
            .SqlQuery<string>($"""SELECT template_key AS "Value" FROM convocatorias.template ORDER BY template_key""")
            .ToListAsync());

        keys.ShouldBe(
        [
            TemplateKey.DirectMessage,
            TemplateKey.EventCancelled,
            TemplateKey.RegistrationConfirmation,
            TemplateKey.RegistrationInvite,
            TemplateKey.SaveTheDate,
        ]);
    }

    [Fact]
    public async Task Seed_ReturnsTheBodiesTheDomainHolds_WhenApplied()
    {
        // La migración inserta lo que dice TemplateSeed, byte por byte: si alguien edita la copia
        // sin despachar una migración nueva, esto lo dice.
        var bodies = await WithContextAsync(context => context.Database
            .SqlQuery<string>($"""SELECT body_html AS "Value" FROM convocatorias.template ORDER BY template_key""")
            .ToListAsync());

        bodies.ShouldBe(
            [.. TemplateSeed.Rows.OrderBy(t => t.TemplateKey, StringComparer.Ordinal).Select(t => t.BodyHtml)]);
    }

    [Fact]
    public async Task Insert_ReturnsAConstraintViolation_WhenTheLanguageIsNotSupported()
    {
        var write = () => WithContextAsync(context => context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO convocatorias.template (template_key, language, subject, body_html, purpose, is_campaign_selectable)
            VALUES ('save_the_date', 'ca', 'x', '<p></p>', 'campaign', false)
            """));

        await Should.ThrowAsync<Exception>(write);
    }

    [Fact]
    public async Task Insert_ReturnsAConstraintViolation_WhenATransactionalRowIsSelectable()
    {
        var write = () => WithContextAsync(context => context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO convocatorias.template (template_key, language, subject, body_html, purpose, is_campaign_selectable)
            VALUES ('otra', 'es', 'x', '<p></p>', 'transactional', true)
            """));

        await Should.ThrowAsync<Exception>(write);
    }
}

using System.Text.RegularExpressions;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Templates;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Templates;

/// <summary>
/// Las reglas del correo al ganador, fila por fila (spec 03 §5.3). La prosa la fija
/// <see cref="PrizeWonTemplateGoldenFileTests"/>; acá solo se afirma estructura.
/// </summary>
public sealed class PrizeWonTemplateSeedTests
{
    private static readonly string[] Placeholders =
        ["codigo", "evento", "fechas", "instrucciones", "nombre", "organizador", "premio"];

    [Fact]
    public void Rows_ReturnsSpanishAndEnglish_WhenRead()
    {
        PrizeWonTemplateSeed.Rows.Select(r => (r.TemplateKey, r.Language)).ShouldBe(
            [
                (RaffleNotificationType.PrizeWon, AllowedLanguage.Espanol),
                (RaffleNotificationType.PrizeWon, AllowedLanguage.Ingles),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Rows_ReturnsNoTemplateTheQuoteMigrationsAlreadySeeded_WhenRead()
    {
        // Misma tabla, misma clave primaria: una clave repetida es un despliegue que se cae.
        var seededEarlier = QuoteTemplateSeed.Rows
            .Concat(QuoteOutcomeTemplateSeed.Rows)
            .Concat(QuoteWithdrawnTemplateSeed.Rows)
            .Select(r => r.TemplateKey);

        seededEarlier.ShouldNotContain(RaffleNotificationType.PrizeWon);
    }

    [Theory]
    [InlineData(AllowedLanguage.Espanol)]
    [InlineData(AllowedLanguage.Ingles)]
    public void Body_ReturnsExactlyTheSixPlaceholders_WhenRead(string language)
    {
        // Una sola bolsa de datos sirve a todos los idiomas, así que las claves son las mismas.
        PlaceholdersIn(Row(language).BodyHtml).ShouldBe(Placeholders);
    }

    [Theory]
    [InlineData(AllowedLanguage.Espanol)]
    [InlineData(AllowedLanguage.Ingles)]
    public void Subject_ReturnsOnlyThePrizeAndTheEvent_WhenRead(string language)
    {
        PlaceholdersIn(Row(language).Subject).ShouldBe(["evento", "premio"]);
    }

    [Theory]
    [InlineData(AllowedLanguage.Espanol)]
    [InlineData(AllowedLanguage.Ingles)]
    public void Body_ReturnsNoLinkSponsorOrValue_WhenRead(string language)
    {
        // El ganador no tiene acceso a la aplicación; el patrocinador es optativo y el dialecto no
        // tiene condicionales; el valor del premio no se manda (spec 03 §5.3).
        var body = Row(language).BodyHtml;

        body.ShouldNotContain("<a ");
        body.ShouldNotContain("href");
        body.ShouldNotContain("http");
        body.ShouldNotContain("{{enlace", Case.Insensitive);
        body.ShouldNotContain("patrocinador", Case.Insensitive);
        body.ShouldNotContain("sponsor", Case.Insensitive);
        body.ShouldNotContain("{{valor", Case.Insensitive);
        body.ShouldNotContain("enlace_baja");
    }

    [Theory]
    [InlineData(AllowedLanguage.Espanol)]
    [InlineData(AllowedLanguage.Ingles)]
    public void Body_ReturnsTheInstructionsKeepingLineBreaks_WhenRead(string language)
    {
        // notification-service escapa el valor, así que los saltos del operador solo sobreviven si
        // el elemento los respeta.
        Row(language).BodyHtml.ShouldContain("<p style=\"white-space:pre-line;\">{{instrucciones}}</p>");
    }

    private static NotificationTemplate Row(string language) =>
        PrizeWonTemplateSeed.Rows.Single(r => r.Language == language);

    private static string[] PlaceholdersIn(string text) =>
        [.. Regex.Matches(text, @"\{\{([^{}]*)\}\}").Select(m => m.Groups[1].Value.Trim()).Distinct().Order(StringComparer.Ordinal)];
}

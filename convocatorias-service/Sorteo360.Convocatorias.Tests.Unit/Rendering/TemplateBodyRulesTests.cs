using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Templates;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Rendering;

/// <summary>
/// Las reglas que cobra <see cref="Templates.TemplateSeedTests"/> sobre la copia real, acá
/// probadas al revés: con cuerpos que las incumplen, para saber que el test de la copia puede
/// fallar. Una regla que nunca falla no protege de nada.
/// </summary>
public sealed class TemplateBodyRulesTests
{
    private const string Subject = "Invitación a {{evento}}";

    #region Marcador fuera del conjunto soportado

    [Fact]
    public void UnsupportedPlaceholders_ReturnsThePlaceholder_WhenTheBodyNamesAnUnsupportedOne()
    {
        // El error de dedo que la regla existe para atrapar.
        var body = "<p>Le invitamos a {{evneto}}.</p>";

        TemplateBodyRules.UnsupportedPlaceholders(Subject, body)
            .ShouldBe(["evneto"]);
    }

    [Fact]
    public void UnsupportedPlaceholders_ReturnsThePlaceholder_WhenTheSubjectNamesAnUnsupportedOne()
    {
        // El asunto se sustituye con el mismo motor que el cuerpo, así que se revisa igual.
        TemplateBodyRules.UnsupportedPlaceholders("Hola {{empresa}}", "<p>{{evento}}</p>")
            .ShouldBe(["empresa"]);
    }

    [Fact]
    public void UnsupportedPlaceholders_ReturnsNone_WhenEverySupportedPlaceholderIsUsed()
    {
        var body = string.Concat(SupportedPlaceholder.All.Keys.Select(key => $"<p>{{{{{key}}}}}</p>"));

        TemplateBodyRules.UnsupportedPlaceholders(string.Empty, body)
            .ShouldBeEmpty();
    }

    [Fact]
    public void UnsupportedPlaceholders_ReturnsNone_WhenTheBodyHasNoPlaceholders()
    {
        TemplateBodyRules.UnsupportedPlaceholders(string.Empty, "<p>Sin marcadores.</p>")
            .ShouldBeEmpty();
    }

    #endregion

    #region La regla invertida del enlace de baja

    [Fact]
    public void CheckUnsubscribeRule_ReturnsMissingOnCampaign_WhenACampaignBodyLacksTheLink()
    {
        TemplateBodyRules.CheckUnsubscribeRule(TemplatePurpose.Campaign, "<p>{{evento}}</p>")
            .ShouldBe(UnsubscribeRuleViolation.MissingOnCampaign);
    }

    [Fact]
    public void CheckUnsubscribeRule_ReturnsPresentOnTransactional_WhenATransactionalBodyHasTheLink()
    {
        TemplateBodyRules.CheckUnsubscribeRule(TemplatePurpose.Transactional, "<p><a href=\"{{enlace_baja}}\">Baja</a></p>")
            .ShouldBe(UnsubscribeRuleViolation.PresentOnTransactional);
    }

    [Fact]
    public void CheckUnsubscribeRule_ReturnsNone_WhenACampaignBodyHasTheLink()
    {
        TemplateBodyRules.CheckUnsubscribeRule(TemplatePurpose.Campaign, "<p><a href=\"{{enlace_baja}}\">Baja</a></p>")
            .ShouldBe(UnsubscribeRuleViolation.None);
    }

    [Fact]
    public void CheckUnsubscribeRule_ReturnsNone_WhenATransactionalBodyLacksTheLink()
    {
        TemplateBodyRules.CheckUnsubscribeRule(TemplatePurpose.Transactional, "<p>{{evento}}</p>")
            .ShouldBe(UnsubscribeRuleViolation.None);
    }

    [Fact]
    public void CheckUnsubscribeRule_ReturnsMissingOnCampaign_WhenTheLinkIsAbsentFromACampaignBody()
    {
        // El enlace vive en el pie del correo. Un asunto que lo nombrara no cumpliría nada.
        TemplateBodyRules.CheckUnsubscribeRule(TemplatePurpose.Campaign, "<p>{{evento}}</p>")
            .ShouldBe(UnsubscribeRuleViolation.MissingOnCampaign);
    }

    #endregion

    #region Valores de evento ausentes

    private static IReadOnlyList<string> MissingFields(
        string subject,
        string bodyHtml,
        Dictionary<string, string> values)
    {
        var result = TemplateBodyRules.CheckEventValues(subject, bodyHtml, values);

        result.IsFailure.ShouldBeTrue();

        return result.Error.ShouldBeOfType<EventValuesMissingError>().Fields;
    }

    [Fact]
    public void CheckEventValues_ReturnsTheMissingFieldNames_WhenEventValuesAreMissing()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["evento"] = "Roadshow Madrid 2026"
        };

        MissingFields(Subject, "<p>{{lugar}} el {{fecha}}</p>", values)
            .ShouldBe(["lugar", "fecha"]);
    }

    [Fact]
    public void CheckEventValues_ReturnsAnErrorCarryingTheFields_WhenEventValuesAreMissing()
    {
        // Quien consume ramifica por el código y pinta su propio español; los campos son el dato
        // con el que lo resuelve.
        var result = TemplateBodyRules.CheckEventValues(Subject, "<p>{{lugar}}</p>", new Dictionary<string, string>());

        var error = result.Error.ShouldBeOfType<EventValuesMissingError>();

        error.Code.ShouldBe(EventValuesMissingError.ErrorCode);
        error.Kind.ShouldBe(Sorteo360.Platform.Errors.ErrorKind.Invalid);
        error.ProblemExtensions["fields"].ShouldBe(new[] { "evento", "lugar" });
    }

    [Fact]
    public void CheckEventValues_ReturnsTheFieldAsMissing_WhenTheEventValueIsBlank()
    {
        // "se celebrará en ." es peor que un lanzamiento rechazado.
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lugar"] = "   "
        };

        MissingFields(string.Empty, "<p>{{lugar}}</p>", values)
            .ShouldBe(["lugar"]);
    }

    [Fact]
    public void CheckEventValues_ReturnsSuccess_WhenOnlyRecipientValuesAreMissing()
    {
        // El grano de persona no rechaza nada: {{nombre}} se cae al saludo neutro y los otros
        // tres los produce este servicio para cada destinatario.
        var body = "<p>{{nombre}} {{enlace_registro}} {{qr}} {{enlace_baja}}</p>";

        TemplateBodyRules.CheckEventValues(string.Empty, body, new Dictionary<string, string>())
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckEventValues_ReturnsSuccess_WhenEveryEventValueIsPresent()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["evento"] = "Roadshow Madrid 2026",
            ["lugar"] = "Madrid, España",
            ["fecha"] = "12 de junio de 2026"
        };

        TemplateBodyRules.CheckEventValues(Subject, "<p>{{lugar}} el {{fecha}}</p>", values)
            .IsSuccess.ShouldBeTrue();
    }

    #endregion
}

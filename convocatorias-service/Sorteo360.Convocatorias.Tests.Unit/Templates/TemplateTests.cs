using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Templates;

/// <summary>
/// Los invariantes que defiende la entidad. Las reglas de marcadores no están acá a propósito:
/// son reglas sobre la copia sembrada y las cobra <see cref="TemplateSeedTests"/>; meterlas en
/// el constructor dejaría sin forma de escribir ese test.
/// </summary>
public sealed class TemplateTests
{
    private static Template Build(
        string language = AllowedLanguage.Espanol,
        string purpose = TemplatePurpose.Campaign,
        bool isCampaignSelectable = true) =>
        new(
            TemplateKey.RegistrationInvite,
            language,
            "Invitación a {{evento}}",
            "<p><a href=\"{{enlace_baja}}\">Baja</a></p>",
            purpose,
            isCampaignSelectable);

    [Fact]
    public void Constructor_ReturnsATemplateWithTheGivenValues_WhenTheInputIsValid()
    {
        var template = Build();

        template.TemplateKey.ShouldBe(TemplateKey.RegistrationInvite);
        template.Language.ShouldBe(AllowedLanguage.Espanol);
        template.Purpose.ShouldBe(TemplatePurpose.Campaign);
        template.IsCampaignSelectable.ShouldBeTrue();
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("pt")]
    [InlineData("it")]
    public void Constructor_ReturnsTheLanguage_WhenItIsOneOfTheSix(string language)
    {
        Build(language: language).Language.ShouldBe(language);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenTheLanguageIsOutsideTheSix()
    {
        Should.Throw<ArgumentException>(() => Build(language: "ca"));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenThePurposeIsOutsideTheTwo()
    {
        Should.Throw<ArgumentException>(() => Build(purpose: "approval"));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenATransactionalTemplateIsSelectable()
    {
        // Su lista se deriva de filas que ya tenemos: nadie la selecciona para una campaña.
        Should.Throw<ArgumentException>(() =>
            Build(purpose: TemplatePurpose.Transactional, isCampaignSelectable: true));
    }

    [Fact]
    public void Constructor_ReturnsANonSelectableTemplate_WhenACampaignIsLeftOutOfThePicker()
    {
        Build(purpose: TemplatePurpose.Campaign, isCampaignSelectable: false)
            .IsCampaignSelectable.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenTheKeyIsBlank(string templateKey)
    {
        Should.Throw<ArgumentException>(() => new Template(
            templateKey, AllowedLanguage.Espanol, "asunto", "<p></p>", TemplatePurpose.Transactional, false));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenTheBodyIsBlank()
    {
        Should.Throw<ArgumentException>(() => new Template(
            TemplateKey.EventCancelled, AllowedLanguage.Espanol, "asunto", "  ", TemplatePurpose.Transactional, false));
    }
}

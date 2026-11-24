using System.Security.Cryptography;
using System.Text;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Templates;

/// <summary>
/// Las reglas de marcadores corridas sobre la copia que siembra la migración. Es el test que
/// convierte un <c>{{evneto}}</c> en una compilación rota en vez de en mil bandejas de entrada:
/// nadie escribe una lista de marcadores en ninguna parte, se escanea del cuerpo.
/// </summary>
public sealed class TemplateSeedTests
{
    public static TheoryData<string, string> SeededRows()
    {
        var data = new TheoryData<string, string>();

        foreach (var template in TemplateSeed.Rows)
            data.Add(template.TemplateKey, template.Language);

        return data;
    }

    private static Template Row(string templateKey, string language) =>
        TemplateSeed.Rows.Single(t => t.TemplateKey == templateKey && t.Language == language);

    #region El conjunto sembrado

    [Fact]
    public void Rows_ReturnsTheFiveSpanishKeys_WhenSeeded()
    {
        TemplateSeed.Rows
            .Select(template => (template.TemplateKey, template.Language))
            .ShouldBe(
            [
                (TemplateKey.SaveTheDate, AllowedLanguage.Espanol),
                (TemplateKey.RegistrationInvite, AllowedLanguage.Espanol),
                (TemplateKey.RegistrationConfirmation, AllowedLanguage.Espanol),
                (TemplateKey.EventCancelled, AllowedLanguage.Espanol),
                (TemplateKey.DirectMessage, AllowedLanguage.Espanol),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Rows_ReturnsOneRowPerKeyAndLanguage_WhenSeeded()
    {
        TemplateSeed.Rows
            .GroupBy(template => (template.TemplateKey, template.Language))
            .ShouldAllBe(group => group.Count() == 1);
    }

    [Fact]
    public void Rows_ReturnsOnlyTheTwoCampaignKeysAsSelectable_WhenSeeded()
    {
        TemplateSeed.Rows
            .Where(template => template.IsCampaignSelectable)
            .Select(template => template.TemplateKey)
            .ShouldBe([TemplateKey.SaveTheDate, TemplateKey.RegistrationInvite], ignoreOrder: true);
    }

    [Fact]
    public void Rows_ReturnsADirectMessageWithNoRegistrationLink_WhenSeeded()
    {
        // Sin evento no hay nada a lo que inscribirse: el mensaje directo lleva su asunto, su texto
        // y el enlace de baja, y ningún marcador de grano de evento (PGTI-174).
        var template = Row(TemplateKey.DirectMessage, AllowedLanguage.Espanol);

        template.Purpose.ShouldBe(TemplatePurpose.Campaign);
        template.Subject.ShouldBe("{{asunto}}");
        PlaceholderScanner.Scan(template.Subject, template.BodyHtml).ShouldBe(
            [SupportedPlaceholder.DirectSubject, SupportedPlaceholder.RecipientName, SupportedPlaceholder.DirectMessage, SupportedPlaceholder.UnsubscribeLink],
            ignoreOrder: true);
    }

    #endregion

    #region Marcadores derivados

    [Theory]
    [MemberData(nameof(SeededRows))]
    public void Rows_ReturnsNoUnsupportedPlaceholders_WhenEachRowIsScanned(string templateKey, string language)
    {
        var template = Row(templateKey, language);

        TemplateBodyRules.UnsupportedPlaceholders(template.Subject, template.BodyHtml)
            .ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(SeededRows))]
    public void Rows_ReturnsNoUnsubscribeViolation_WhenEachRowIsChecked(string templateKey, string language)
    {
        var template = Row(templateKey, language);

        TemplateBodyRules.CheckUnsubscribeRule(template.Purpose, template.BodyHtml)
            .ShouldBe(UnsubscribeRuleViolation.None);
    }

    [Fact]
    public void Rows_ReturnsTheUnsubscribeLink_WhenThePurposeIsCampaign()
    {
        var campaigns = TemplateSeed.Rows.Where(template => template.Purpose == TemplatePurpose.Campaign);

        campaigns.ShouldNotBeEmpty();
        campaigns.ShouldAllBe(template => template.BodyHtml.Contains("{{enlace_baja}}"));
    }

    [Fact]
    public void Rows_ReturnsNoUnsubscribeLink_WhenThePurposeIsTransactional()
    {
        // Una transaccional contesta al propio acto de la persona, así que no hay de qué darse
        // de baja: nadie se da de baja de que le avisen de que el evento al que se inscribió
        // se canceló.
        var transactional = TemplateSeed.Rows.Where(template => template.Purpose == TemplatePurpose.Transactional);

        transactional.ShouldNotBeEmpty();
        transactional.ShouldAllBe(template => !template.BodyHtml.Contains("{{enlace_baja}}"));
    }

    #endregion

    #region La copia está congelada

    /// <summary>
    /// Huella de la copia sembrada. Está clavada a mano: no se recalcula sola, porque su trabajo
    /// es justamente romperse cuando alguien cambia la copia.
    /// </summary>
    private const string SeededCopyFingerprint = "448cfa041e99be9067c9afd4f27e423b06e60d128739ef9ec02daff7946a0f1f";

    [Fact]
    public void Rows_ReturnsTheFrozenFingerprint_WhenHashed()
    {
        // La migración AddTemplates inserta lo que dice TemplateSeed, así que editar esta clase
        // cambiaría lo que recibe una base nueva sin tocar una base existente: la misma
        // divergencia silenciosa por la que no se usa HasData. Un arreglo de copia se despacha
        // como una migración nueva con sus propios valores, y entonces se actualiza esta huella.
        var payload = string.Join('\u001f', TemplateSeed.Rows.SelectMany(template => new[]
        {
            template.TemplateKey,
            template.Language,
            template.Subject,
            template.BodyHtml,
            template.Purpose,
            template.IsCampaignSelectable.ToString()
        }));

        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

        fingerprint.ShouldBe(
            SeededCopyFingerprint,
            "La copia sembrada cambió. Si el cambio es intencional, despache una migración nueva con los valores nuevos y actualice esta huella.");
    }

    #endregion

    #region El arte

    [Theory]
    [MemberData(nameof(SeededRows))]
    public void Rows_ReturnsTheArtworkAsAnHttpsImage_WhenEachBodyIsRead(string templateKey, string language)
    {
        // No hay columna arte_url, ni endpoint de subida, ni stored_file: convocatorias no es
        // consumidor de ADR-0014. El arte se cambia como se cambia la copia.
        var template = Row(templateKey, language);

        template.BodyHtml.ShouldContain("<img src=\"https://");
    }

    #endregion
}

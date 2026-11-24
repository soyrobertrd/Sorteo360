using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Errors;

/// <summary>
/// La plantilla que falta en un idioma (spec <c>03</c> §4.1). Un <c>400</c> y no un <c>404</c>, que
/// se leería como "esa campaña no está", y distinto del fallo del worker, que es lo que esta
/// negativa existe para adelantar.
/// </summary>
public sealed class CampaignTemplateNotFoundErrorTests
{
    [Fact]
    public void ProblemExtensions_ReturnsTheTemplateKeyAndTheLanguage_WhenAPairHasNoTemplate()
    {
        var error = new CampaignTemplateNotFoundError("registration_invite", "en");

        error.Kind.ShouldBe(ErrorKind.Invalid);
        error.Code.ShouldBe("CAMPAIGN_TEMPLATE_NOT_FOUND");
        error.Code.ShouldNotBe(CampaignNotRenderableError.ErrorCode);
        error.ProblemExtensions["templateKey"].ShouldBe("registration_invite");
        error.ProblemExtensions["language"].ShouldBe("en");
    }
}

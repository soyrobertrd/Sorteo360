using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Templates;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// La regla del mensaje directo (PGTI-174): una campaña no tiene evento si y solo si sale con
/// <c>direct_message</c>, y entonces lleva asunto y texto; una de evento no lleva ninguno.
/// </summary>
public sealed class CampaignDirectMessageTests
{
    private static readonly Guid EventId = Guid.NewGuid();

    [Fact]
    public void Check_ReturnsNull_WhenADirectMessageHasNoEventAndCarriesItsText()
    {
        CampaignDirectMessage.Check(null, TemplateKey.DirectMessage, "Asunto", "Texto").ShouldBeNull();
    }

    [Fact]
    public void Check_ReturnsNull_WhenAnEventCampaignCarriesNoText()
    {
        CampaignDirectMessage.Check(EventId, TemplateKey.RegistrationInvite, null, null).ShouldBeNull();
    }

    public static TheoryData<Guid?, string, string?, string?> Mismatches() => new()
    {
        // Sin evento y con una plantilla de evento: no hay con qué rellenar {{evento}}.
        { null, TemplateKey.RegistrationInvite, null, null },
        // Un mensaje directo con evento.
        { EventId, TemplateKey.DirectMessage, "Asunto", "Texto" },
        // Un mensaje directo sin asunto, o con el texto en blanco.
        { null, TemplateKey.DirectMessage, null, "Texto" },
        { null, TemplateKey.DirectMessage, "Asunto", "   " },
        // Una campaña de evento con texto propio.
        { EventId, TemplateKey.SaveTheDate, "Asunto", null },
    };

    [Theory]
    [MemberData(nameof(Mismatches))]
    public void Check_ReturnsTheMismatchError_WhenTheRuleIsBroken(
        Guid? eventId,
        string templateKey,
        string? subject,
        string? message)
    {
        var error = CampaignDirectMessage.Check(eventId, templateKey, subject, message);

        error.ShouldNotBeNull().Code.ShouldBe(CampaignDirectMessageMismatchError.ErrorCode);
    }
}

using Sorteo360.Convocatorias.Core.Campaigns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// Las guardas de la entidad. La entidad valida sus conjuntos cerrados y deja las reglas de
/// flujo a <see cref="CampaignState"/>, que es donde se pueden cobrar sin construir una campaña
/// para cada pregunta.
/// </summary>
public sealed class CampaignTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Campaign Build(string state = CampaignState.Draft, string language = "es") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TemplateKeyForTests,
            language,
            state,
            launchedById: null,
            launchedAt: null,
            sentAt: null,
            createdAt: Now,
            updatedAt: Now,
            createdById: UserId,
            updatedById: UserId);

    private const string TemplateKeyForTests = "registration_invite";

    [Fact]
    public void Campaign_ReturnsDraftNotSendingNotTerminal_WhenTheStateIsDraft()
    {
        var campaign = Build();

        campaign.IsDraft.ShouldBeTrue();
        campaign.IsSending.ShouldBeFalse();
        campaign.IsTerminal.ShouldBeFalse();
    }

    [Fact]
    public void Campaign_ReturnsSendingNeitherDraftNorTerminal_WhenTheStateIsSending()
    {
        var campaign = Build(CampaignState.Sending);

        campaign.IsDraft.ShouldBeFalse();
        campaign.IsSending.ShouldBeTrue();
        campaign.IsTerminal.ShouldBeFalse();
    }

    [Theory]
    [InlineData(CampaignState.Sent)]
    [InlineData(CampaignState.Cancelled)]
    public void Campaign_ReturnsTerminal_WhenTheStateIsSentOrCancelled(string state) =>
        Build(state).IsTerminal.ShouldBeTrue();

    [Theory]
    [InlineData("xx")]
    [InlineData("ES-es")]
    public void Campaign_ThrowsArgumentException_WhenTheLanguageIsOutsideTheCatalogue(string language) =>
        Should.Throw<ArgumentException>(() => Build(language: language));

    [Theory]
    [InlineData("pending_approval")]
    [InlineData("approved")]
    public void Campaign_ThrowsArgumentException_WhenTheStateIsOutsideTheCatalogue(string state) =>
        Should.Throw<ArgumentException>(() => Build(state));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Campaign_ThrowsArgumentException_WhenTheTemplateKeyIsBlank(string templateKey) =>
        Should.Throw<ArgumentException>(() => new Campaign(
            Guid.NewGuid(),
            Guid.NewGuid(),
            templateKey,
            "es",
            CampaignState.Draft,
            null, null, null, Now, Now, UserId, UserId));
}

using Sorteo360.Convocatorias.Core.Campaigns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// La tabla de transiciones de la spec <c>03</c> §2, cobrada en las dos direcciones: las cuatro
/// que existen pasan, y <b>todas</b> las que no existen se niegan. Enumerar las negativas a mano
/// dejaría fuera justo la que alguien agregue por descuido, así que se generan por producto
/// cartesiano y se le resta la tabla.
/// </summary>
public sealed class CampaignStateTests
{
    [Theory]
    [InlineData(CampaignState.Draft, CampaignState.Sending)]
    [InlineData(CampaignState.Draft, CampaignState.Cancelled)]
    [InlineData(CampaignState.Sending, CampaignState.Sent)]
    [InlineData(CampaignState.Sending, CampaignState.Cancelled)]
    public void CanTransition_ReturnsTrue_WhenTheTransitionIsInTheTable(string from, string to) =>
        CampaignState.CanTransition(from, to).ShouldBeTrue();

    [Fact]
    public void CanTransition_ReturnsFalse_WhenTheTransitionIsOutsideTheTable()
    {
        var allowed = new HashSet<(string From, string To)>
        {
            (CampaignState.Draft, CampaignState.Sending),
            (CampaignState.Draft, CampaignState.Cancelled),
            (CampaignState.Sending, CampaignState.Sent),
            (CampaignState.Sending, CampaignState.Cancelled),
        };

        var refused = CampaignState.All
            .SelectMany(_ => CampaignState.All, (source, target) => (source, target))
            .Where(pair => !allowed.Contains(pair));

        foreach (var (source, target) in refused)
        {
            CampaignState.CanTransition(source, target)
                .ShouldBeFalse($"'{source}' no debería poder pasar a '{target}'");
        }
    }

    /// <summary>
    /// <c>pending_approval</c> se fue con el paso de aprobación, y con él los actos de someter y
    /// retirar. Se comprueba como ausencia para que nadie lo reintroduzca sin discutirlo.
    /// </summary>
    [Theory]
    [InlineData("pending_approval")]
    [InlineData("submitted")]
    [InlineData("approved")]
    [InlineData("rejected")]
    public void All_ReturnsNoApprovalEraState_WhenTheStateIsRetired(string retired)
    {
        CampaignState.All.ShouldNotContain(retired);
        CampaignState.Canonicalize(retired).ShouldBeNull();
    }

    [Fact]
    public void All_ReturnsExactlyFourStates_WhenCalled() =>
        CampaignState.All.ShouldBe(["draft", "sending", "sent", "cancelled"], ignoreOrder: true);

    [Theory]
    [InlineData(CampaignState.Sent)]
    [InlineData(CampaignState.Cancelled)]
    public void IsTerminal_ReturnsTrueWithNoTransitionOut_WhenTheStateIsSentOrCancelled(string terminal)
    {
        CampaignState.IsTerminal(terminal).ShouldBeTrue();

        foreach (var to in CampaignState.All)
            CampaignState.CanTransition(terminal, to).ShouldBeFalse();
    }

    [Theory]
    [InlineData(CampaignState.Draft)]
    [InlineData(CampaignState.Sending)]
    public void IsTerminal_ReturnsFalse_WhenTheStateIsDraftOrSending(string live) =>
        CampaignState.IsTerminal(live).ShouldBeFalse();

    [Theory]
    [InlineData("DRAFT", CampaignState.Draft)]
    [InlineData("Sending", CampaignState.Sending)]
    [InlineData("sent", CampaignState.Sent)]
    public void Canonicalize_ReturnsTheCatalogueSpelling_WhenTheStateIsKnownInAnyCase(string input, string expected) =>
        CampaignState.Canonicalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sendinng")]
    public void Canonicalize_ReturnsNull_WhenTheInputIsOutsideTheCatalogue(string? input) =>
        CampaignState.Canonicalize(input).ShouldBeNull();
}

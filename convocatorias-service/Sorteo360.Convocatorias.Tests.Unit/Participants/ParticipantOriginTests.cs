using Sorteo360.Convocatorias.Core.Participants;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Participants;

/// <summary>
/// La regla de la procedencia (spec <c>07</c> §3.2), una sola para la unión de participantes y para
/// la lista de check-ins de la rifa (spec <c>09</c> §7.1).
/// </summary>
public sealed class ParticipantOriginTests
{
    [Fact]
    public void Of_ReturnsInvited_WhenTheAddressIsARecipientAndAlsoCameThroughALink()
    {
        ParticipantOrigin.Of(isRecipient: true, cameThroughLink: true).ShouldBe(ParticipantOrigin.Invited);
    }

    [Fact]
    public void Of_ReturnsInvited_WhenTheAddressIsARecipientWithoutALink()
    {
        ParticipantOrigin.Of(isRecipient: true, cameThroughLink: false).ShouldBe(ParticipantOrigin.Invited);
    }

    [Fact]
    public void Of_ReturnsReferred_WhenOnlyTheLinkIsThere()
    {
        ParticipantOrigin.Of(isRecipient: false, cameThroughLink: true).ShouldBe(ParticipantOrigin.Referred);
    }

    [Fact]
    public void Of_ReturnsWalkIn_WhenNeitherIsThere()
    {
        ParticipantOrigin.Of(isRecipient: false, cameThroughLink: false).ShouldBe(ParticipantOrigin.WalkIn);
    }
}

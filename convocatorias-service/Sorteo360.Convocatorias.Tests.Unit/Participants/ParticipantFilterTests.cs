using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Participants;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Participants;

/// <summary>Los filtros de la lista de participantes sobre la fila ya unida (spec <c>07</c> §3.3).</summary>
public sealed class ParticipantFilterTests
{
    private static readonly Participant Invitee = new(
        "juan@empresa.com", "Juan", "Pérez", null, ParticipantOrigin.Invited, RecipientSendStatus.HandedOff,
        Registered: false, null, null, null, [Guid.NewGuid()], false, false);

    private static readonly Participant WalkIn = Invitee with
    {
        Email = "luis@acme.com", FirstName = "Luis", LastName = "Peña",
        Origin = ParticipantOrigin.WalkIn, SendStatus = ParticipantSendStatus.None, Registered = true, CampaignIds = [],
    };

    [Fact]
    public void Matches_ReturnsTrueForEveryone_WhenNoFilterIsSet()
    {
        ParticipantFilter.None.Matches(Invitee).ShouldBeTrue();
        ParticipantFilter.None.Matches(WalkIn).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ParticipantOrigin.Invited, true, false)]
    [InlineData(ParticipantOrigin.WalkIn, false, true)]
    [InlineData(ParticipantOrigin.Referred, false, false)]
    // Un valor fuera del catálogo no devuelve todo: devuelve nada, como el filtro de estado de campañas.
    [InlineData("invitado", false, false)]
    public void Matches_ReturnsOnlyThatOrigin_WhenOriginIsSet(string origin, bool invitee, bool walkIn)
    {
        var filter = new ParticipantFilter(Origin: origin);

        filter.Matches(Invitee).ShouldBe(invitee);
        filter.Matches(WalkIn).ShouldBe(walkIn);
    }

    [Theory]
    [InlineData(RecipientSendStatus.HandedOff, true, false)]
    [InlineData(ParticipantSendStatus.None, false, true)]
    [InlineData(RecipientSendStatus.Suppressed, false, false)]
    public void Matches_ReturnsOnlyThatSendStatus_WhenSendStatusIsSet(string sendStatus, bool invitee, bool walkIn)
    {
        var filter = new ParticipantFilter(SendStatus: sendStatus);

        filter.Matches(Invitee).ShouldBe(invitee);
        filter.Matches(WalkIn).ShouldBe(walkIn);
    }

    [Fact]
    public void Matches_ReturnsOnlyThatSide_WhenRegisteredIsSet()
    {
        new ParticipantFilter(Registered: true).Matches(WalkIn).ShouldBeTrue();
        new ParticipantFilter(Registered: true).Matches(Invitee).ShouldBeFalse();
        new ParticipantFilter(Registered: false).Matches(Invitee).ShouldBeTrue();
    }

    /// <summary>
    /// Subcadena sobre el nombre y la dirección, sin mayúsculas ni tildes: quien busca desde un
    /// teclado escribe "pena" y espera encontrar a "Peña".
    /// </summary>
    [Theory]
    [InlineData("pena", false, true)]
    [InlineData("PÉREZ", true, false)]
    [InlineData("juan pérez", true, false)]
    [InlineData("@acme", false, true)]
    [InlineData("  luis ", false, true)]
    [InlineData("nadie", false, false)]
    public void Matches_ReturnsNameAndEmailHits_WhenSearchIsSet(string search, bool invitee, bool walkIn)
    {
        var filter = new ParticipantFilter(Search: search);

        filter.Matches(Invitee).ShouldBe(invitee);
        filter.Matches(WalkIn).ShouldBe(walkIn);
    }
}

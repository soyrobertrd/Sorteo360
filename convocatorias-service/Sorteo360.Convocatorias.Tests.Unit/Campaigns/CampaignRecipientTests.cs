using Sorteo360.Convocatorias.Core.Campaigns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// Las guardas de las dos filas que cuelgan de una campaña.
/// </summary>
public sealed class CampaignRecipientTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static CampaignRecipient BuildRecipient(
        string sendStatus = RecipientSendStatus.Pending,
        string email = "ana@acme.com") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ana",
            "Pérez",
            email,
            sendStatus,
            failureReason: null,
            registrationTokenHash: null,
            unsubscribeTokenHash: null,
            unsubscribedAt: null,
            unsubscribeSyncedAt: null,
            createdAt: Now,
            updatedAt: Now);

    [Fact]
    public void CampaignRecipient_ReturnsPending_WhenNew() =>
        BuildRecipient().IsPending.ShouldBeTrue();

    [Fact]
    public void CampaignRecipient_ReturnsTheSnapshotAsFullName_WhenBuilt() =>
        BuildRecipient().FullName.ShouldBe("Ana Pérez");

    /// <summary>
    /// La dirección nunca falta: la selección solo materializa a quien el CRM declara
    /// contactable, y sin dirección no lo es. Una fila sin correo sería una a la que no se le
    /// puede escribir contada como si sí.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CampaignRecipient_ThrowsArgumentException_WhenTheAddressIsBlank(string email) =>
        Should.Throw<ArgumentException>(() => BuildRecipient(email: email));

    [Fact]
    public void CampaignRecipient_ThrowsArgumentException_WhenTheSendStatusIsOutsideTheCatalogue() =>
        Should.Throw<ArgumentException>(() => BuildRecipient("registered"));

    /// <summary>
    /// La exclusión sí admite correo nulo, y es justo el caso <c>no_email</c>: se guarda para
    /// poder contestar quién falta, aunque no haya dirección que enseñar.
    /// </summary>
    [Fact]
    public void CampaignExclusion_ReturnsANullAddress_WhenTheReasonIsNoEmail()
    {
        var exclusion = new CampaignExclusion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Luis",
            "Gómez",
            email: null,
            ExclusionReason.NoEmail,
            Now);

        exclusion.Email.ShouldBeNull();
        exclusion.FullName.ShouldBe("Luis Gómez");
    }

    [Fact]
    public void CampaignExclusion_ThrowsArgumentException_WhenTheReasonIsOutsideTheCatalogue() =>
        Should.Throw<ArgumentException>(() => new CampaignExclusion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Luis",
            "Gómez",
            "luis@acme.com",
            "hard_bounced",
            Now));
}

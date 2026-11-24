using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Participants;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Participants;

/// <summary>
/// La unión de destinatarios e inscripciones en una fila por persona (spec <c>07</c> §3). La
/// clave es la dirección recortada y en minúsculas, y nada más (§3.1).
/// </summary>
public sealed class ParticipantMergeTests
{
    private static readonly Guid CampaignOne = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid CampaignTwo = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 10, 14, 0, TimeSpan.Zero);

    [Fact]
    public void Merge_ReturnsOneInvitedRowWithBothCampaigns_WhenTwoCampaignsInvitedTheSamePerson()
    {
        var rows = ParticipantMerge.Merge(
            [
                Recipient(CampaignOne, "juan@empresa.com"),
                Recipient(CampaignTwo, "  JUAN@Empresa.com "),
            ],
            []);

        var row = rows.ShouldHaveSingleItem();
        row.Origin.ShouldBe(ParticipantOrigin.Invited);
        row.CampaignIds.ShouldBe([CampaignOne, CampaignTwo]);
        row.Registered.ShouldBeFalse();
    }

    [Fact]
    public void Merge_ReturnsAWalkInWithSendStatusNone_WhenARegistrationHasNoInvitationBehindIt()
    {
        var rows = ParticipantMerge.Merge([], [Registration("luis@acme.com", firstName: "Luis", lastName: "Peña")]);

        rows.ShouldHaveSingleItem().ShouldBe(new Participant(
            "luis@acme.com", "Luis", "Peña", "Empresa SRL",
            ParticipantOrigin.WalkIn, ParticipantSendStatus.None,
            Registered: true, RegisteredAt: Now, Reference: "REF-2026-00427", ReferredByName: null,
            CampaignIds: rows[0].CampaignIds, CrmSyncPending: false, DetailsDiffer: false));
        rows[0].CampaignIds.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_ReturnsAReferredRowNamingTheInvitee_WhenSomeoneRegisteredThroughAForwardedLink()
    {
        var rows = ParticipantMerge.Merge(
            [Recipient(CampaignOne, "ana@empresa.com", firstName: "Ana", lastName: "Gómez")],
            [Registration("colega@empresa.com", referredByName: "Ana Gómez", firstName: "Pedro")]);

        rows.Count.ShouldBe(2);
        var referred = rows.Single(r => r.Email == "colega@empresa.com");
        referred.Origin.ShouldBe(ParticipantOrigin.Referred);
        referred.ReferredByName.ShouldBe("Ana Gómez");
        referred.SendStatus.ShouldBe(ParticipantSendStatus.None);
        referred.CampaignIds.ShouldBeEmpty();
        rows.Single(r => r.Email == "ana@empresa.com").Registered.ShouldBeFalse();
    }

    /// <summary>
    /// La invitada que abre su propio enlace sigue siendo invitada: el enlace la nombra a ella, así
    /// que no hay a quién atribuirle nada.
    /// </summary>
    [Fact]
    public void Merge_ReturnsOneInvitedRegisteredRow_WhenTheInviteeRegisteredAtTheSameAddress()
    {
        var rows = ParticipantMerge.Merge(
            [Recipient(CampaignOne, "juan@empresa.com")],
            [Registration("Juan@Empresa.com ", referredByName: "Juan Pérez")]);

        var row = rows.ShouldHaveSingleItem();
        row.Origin.ShouldBe(ParticipantOrigin.Invited);
        row.SendStatus.ShouldBe(RecipientSendStatus.HandedOff);
        row.Registered.ShouldBeTrue();
        row.Reference.ShouldBe("REF-2026-00427");
        row.Company.ShouldBe("Empresa SRL");
        row.ReferredByName.ShouldBeNull();
        row.CampaignIds.ShouldBe([CampaignOne]);
    }

    /// <summary>
    /// <b>Esto no es un error, y no hay que arreglarlo.</b> La clave es la dirección y nada más
    /// (spec <c>07</c> §3.1): el CRM decretó que una dirección es una persona, así que
    /// <c>juan@empresa.com</c> y <c>juan@gmail.com</c> son dos personas aunque se llamen igual.
    /// Quien fue invitado en una y se inscribió en la otra son dos filas, una invitada sin
    /// inscribir y una entrada libre, y <c>notRegistered</c> puede contar de más por eso. Juntarlas
    /// por nombre fundiría en silencio a dos personas distintas, que es el error que no se ve.
    /// </summary>
    [Fact]
    public void Merge_ReturnsTwoRows_WhenSomeoneInvitedAtOneAddressRegistersAtAnother()
    {
        var rows = ParticipantMerge.Merge(
            [Recipient(CampaignOne, "juan@empresa.com")],
            [Registration("juan@gmail.com")]);

        rows.Count.ShouldBe(2);
        var invitee = rows.Single(r => r.Email == "juan@empresa.com");
        invitee.Origin.ShouldBe(ParticipantOrigin.Invited);
        invitee.Registered.ShouldBeFalse();
        var walkIn = rows.Single(r => r.Email == "juan@gmail.com");
        walkIn.Origin.ShouldBe(ParticipantOrigin.WalkIn);
        walkIn.Registered.ShouldBeTrue();
    }

    /// <summary>
    /// Varias campañas, un solo estado: gana el que más cerca dejó a la persona del correo. Una
    /// vez entregada, lo que pasó con otra campaña no cambia que le escribimos.
    /// </summary>
    [Theory]
    [InlineData(RecipientSendStatus.Suppressed, RecipientSendStatus.HandedOff, RecipientSendStatus.HandedOff)]
    [InlineData(RecipientSendStatus.Failed, RecipientSendStatus.Pending, RecipientSendStatus.Pending)]
    [InlineData(RecipientSendStatus.Suppressed, RecipientSendStatus.Failed, RecipientSendStatus.Failed)]
    [InlineData(RecipientSendStatus.HandedOff, RecipientSendStatus.Pending, RecipientSendStatus.HandedOff)]
    public void Merge_ReturnsTheFurthestSendStatus_WhenCampaignsDisagree(string first, string second, string expected)
    {
        var rows = ParticipantMerge.Merge(
            [Recipient(CampaignOne, "juan@empresa.com", first), Recipient(CampaignTwo, "juan@empresa.com", second)],
            []);

        rows.ShouldHaveSingleItem().SendStatus.ShouldBe(expected);
    }

    /// <summary>
    /// Apellido, nombre y dirección (§3.3). La dirección es lo que vuelve total el orden: dos
    /// "Juan Pérez" no pueden quedar empatados, o la página dos repetiría uno de la uno.
    /// </summary>
    [Fact]
    public void Merge_ReturnsRowsByLastNameThenFirstNameThenEmail_WhenNamesCollide()
    {
        var rows = ParticipantMerge.Merge(
            [
                Recipient(CampaignOne, "z@acme.com", firstName: "Juan", lastName: "Pérez"),
                Recipient(CampaignOne, "a@acme.com", firstName: "Juan", lastName: "Pérez"),
                Recipient(CampaignOne, "m@acme.com", firstName: "Ana", lastName: "Pérez"),
                Recipient(CampaignOne, "b@acme.com", firstName: "Zoe", lastName: "Álvarez"),
            ],
            [Registration("c@acme.com", firstName: "Carlos", lastName: "gómez")]);

        rows.Select(r => r.Email).ShouldBe(["b@acme.com", "c@acme.com", "m@acme.com", "a@acme.com", "z@acme.com"]);
    }

    [Fact]
    public void Merge_ReturnsTheRegistrationCheckIn_WhenTheRegistrationHasOne()
    {
        var checkedInAt = Now.AddDays(3);

        var rows = ParticipantMerge.Merge(
            [Recipient(CampaignOne, "juan@empresa.com")],
            [Registration("juan@empresa.com") with { CheckedInAt = checkedInAt }]);

        rows.ShouldHaveSingleItem().CheckedInAt.ShouldBe(checkedInAt);
    }

    [Fact]
    public void Merge_ReturnsNoCheckIn_WhenTheInviteeHasNoRegistration()
    {
        var rows = ParticipantMerge.Merge([Recipient(CampaignOne, "juan@empresa.com")], []);

        rows.ShouldHaveSingleItem().CheckedInAt.ShouldBeNull();
    }

    private static RecipientSource Recipient(
        Guid campaignId,
        string email,
        string sendStatus = RecipientSendStatus.HandedOff,
        string firstName = "Juan",
        string lastName = "Pérez") =>
        new(campaignId, firstName, lastName, email, sendStatus, Now);

    private static RegistrationSource Registration(
        string email,
        string? referredByName = null,
        string firstName = "Juan",
        string lastName = "Pérez") =>
        new(firstName, lastName, email, "Empresa SRL", "REF-2026-00427", Now, referredByName,
            CrmSyncPending: false, DetailsDiffer: false);
}

using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// El pipeline que convierte la lista de la puerta en el bombo (spec 01 §2, §6). Cada motivo se
/// prueba solo y después contra su vecino, porque el orden es lo que hace que los motivos no se
/// pisen y que la puerta cuadre con calificados más no calificados.
/// <para>
/// Fuera de la región del CRM, el CRM contesta por todos y no veta a nadie (<see cref="Classify"/>).
/// </para>
/// </summary>
public sealed class QualificationTests
{
    private static readonly Maybe<AwardsList> NoList = Maybe<AwardsList>.None;

    #region Un motivo por paso

    [Fact]
    public void Classify_ReturnsQualified_WhenAnInviteePassesEveryStep()
    {
        var result = Classify([Invitee("ana@agencia.com")], NoList);

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBeNull();
        result.Attendees[0].Qualified.ShouldBeTrue();
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenTheAttendeeWasReferred()
    {
        var referred = Invitee("colega@agencia.com") with { Origin = AttendeeOrigin.Referred };

        var result = Classify([referred], NoList);

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenTheAttendeeWasAWalkIn()
    {
        var walkIn = Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn };

        var result = Classify([walkIn], NoList);

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenTheAttendeeHasNoCrmContact()
    {
        var unresolved = Invitee("luis@agencia.com") with { ContactId = null };

        var result = Classify([unresolved], NoList);

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    [Fact]
    public void Classify_ReturnsNotAwardsMember_WhenAListIsInForceWithoutTheEmail()
    {
        var result = Classify([Invitee("ana@agencia.com")], ListOf("otra@agencia.com"));

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBe(ExclusionReason.NotAwardsMember);
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenAListIsInForceWithTheEmail()
    {
        var result = Classify([Invitee("ana@agencia.com")], ListOf("ana@agencia.com"));

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ana@mitur.do")]
    [InlineData("ana@agencia.com.do")]
    [InlineData("ana@sorteo360.local")]
    [InlineData("ana@unibe.edu.do")]
    [InlineData("x@dominio.com")]
    [InlineData("x@mail.do.com")]
    [InlineData("do@agencia.com")]
    public void Classify_ReturnsQualified_WhenDomainEndsInDotDo(string email)
    {
        var result = Classify([Invitee(email)], NoList);

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    #endregion

    #region Gana el primer motivo

    [Fact]
    public void Classify_ReturnsQualified_WhenAWalkInAlsoHasNoContact()
    {
        var attendee = Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn, ContactId = null };

        var result = Classify([attendee], NoList);

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    [Fact]
    public void Classify_ReturnsNotAwardsMember_WhenAnAttendeeWithNoContactIsAlsoNotAMember()
    {
        var attendee = Invitee("luis@agencia.com") with { ContactId = null };

        var result = Classify([attendee], ListOf("otra@agencia.com"));

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBe(ExclusionReason.NotAwardsMember);
    }

    [Fact]
    public void Classify_ReturnsNotAwardsMember_WhenANonMemberAlsoHasADotDoAddress()
    {
        var result = Classify([Invitee("ana@sorteo360.local")], ListOf("otra@agencia.com"));

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBe(ExclusionReason.NotAwardsMember);
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenAWalkInAlsoHasADotDoAddress()
    {
        var attendee = Invitee("pedro@sorteo360.local") with { Origin = AttendeeOrigin.WalkIn };

        var result = Classify([attendee], NoList);

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    #endregion

    #region Sin lista no es lista vacía

    [Fact]
    public void Classify_SkipsTheAwardsStep_WhenNoListIsInForce()
    {
        var result = Classify(
            [Invitee("ana@agencia.com"), Invitee("luis@agencia.com")],
            NoList);

        result.Attendees.ShouldAllBe(a => a.Qualified);
    }

    [Fact]
    public void Classify_QualifiesNobody_WhenTheListInForceMatchesNobody()
    {
        var result = Classify(
            [Invitee("ana@agencia.com"), Invitee("luis@agencia.com")],
            ListOf());

        result.Qualified.ShouldBe(0);
        result.Attendees.ShouldAllBe(a => a.ExclusionReason == ExclusionReason.NotAwardsMember);
    }

    #endregion

    #region La puerta cuadra

    [Fact]
    public void Classify_ReturnsCountsThatAddUpToTheDoor_WhenEveryReasonIsPresent()
    {
        Attendee[] door =
        [
            Invitee("ana@agencia.com"),
            Invitee("colega@agencia.com") with { Origin = AttendeeOrigin.Referred },
            Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn },
            Invitee("luis@agencia.com") with { ContactId = null },
            Invitee("marta@agencia.com"),
            Invitee("jose@sorteo360.local"),
        ];
        var list = ListOf("ana@agencia.com", "colega@agencia.com", "pedro@agencia.com", "luis@agencia.com", "jose@sorteo360.local");

        var result = Classify(door, list);

        result.CheckedIn.ShouldBe(6);
        (result.Qualified + result.NotQualified).ShouldBe(result.CheckedIn);
        result.Qualified.ShouldBe(5);
        result.Attendees.Select(a => a.Attendee).ShouldBe(door);
    }

    [Fact]
    public void Classify_ReturnsZeroCounts_WhenNobodyCheckedIn()
    {
        var result = Classify([], NoList);

        result.CheckedIn.ShouldBe(0);
        result.Qualified.ShouldBe(0);
        result.NotQualified.ShouldBe(0);
    }

    #endregion

    #region La lista negra del CRM

    [Theory]
    [InlineData("do_not_invite")]
    [InlineData("un_motivo_que_el_crm_agregue_mañana")]
    public void Classify_ReturnsDoNotInvite_WhenTheCrmSaysTheContactMayNotBeInvited(string reason)
    {
        // Un motivo nuevo del CRM deja fuera sin que haya que desplegar nada: el portazo es
        // canBeInvited, el motivo solo le pone nombre (spec 01 §3).
        var ana = Invitee("ana@agencia.com");

        var result = Qualification.Classify([ana], NoList, [], Verdicts((ana, reason)));

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBe(ExclusionReason.DoNotInvite);
    }

    [Fact]
    public void Classify_ReturnsSuspended_WhenTheCrmSuspendedTheContact()
    {
        var ana = Invitee("ana@agencia.com");

        var result = Qualification.Classify([ana], NoList, [], Verdicts((ana, "suspended")));

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBe(ExclusionReason.Suspended);
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenTheCrmSaysTheContactMayBeInvited()
    {
        // Un null es canBeInvited: el estado del correo —opted_out, no_email— no entra (spec 01 §3).
        var ana = Invitee("ana@agencia.com");

        var result = Qualification.Classify([ana], NoList, [], Verdicts((ana, null)));

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    [Fact]
    public void Classify_ReturnsSuspendedAndOverruled_WhenASuspendedContactWasManuallyIncluded()
    {
        var ana = Invitee("ana@agencia.com");
        var include = Override("ana@agencia.com", AttendeeOverrideDecision.Included);

        var result = Qualification.Classify([ana], NoList, [include], Verdicts((ana, "suspended")));

        var classified = result.Attendees.ShouldHaveSingleItem();
        classified.Qualified.ShouldBeFalse();
        classified.ExclusionReason.ShouldBe(ExclusionReason.Suspended);
        classified.OverruledReason.ShouldBe(ExclusionReason.Suspended);
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenTheCrmDoesNotReturnTheContact()
    {
        var ana = Invitee("ana@agencia.com");

        var result = Qualification.Classify([ana], NoList, [], Verdicts());

        result.Attendees.ShouldHaveSingleItem().Qualified.ShouldBeTrue();
    }

    [Fact]
    public void Classify_SkipsOnlyTheBlacklistStep_WhenTheCrmDidNotAnswer()
    {
        Attendee[] door =
        [
            Invitee("ana@agencia.com"),
            Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn },
            Invitee("luis@agencia.com") with { ContactId = null },
            Invitee("jose@sorteo360.local"),
            Invitee("marta@agencia.com"),
        ];

        var result = Qualification.Classify(
            door,
            NoList,
            [Override("marta@agencia.com", AttendeeOverrideDecision.Excluded)],
            Maybe<IReadOnlyDictionary<Guid, string?>>.None);

        result.Attendees.Select(a => a.ExclusionReason).ShouldBe(
        [
            null,
            null,
            null,
            null,
            ExclusionReason.ManuallyExcluded,
        ]);
    }

    [Fact]
    public void Classify_ReturnsDoNotInvite_WhenABlacklistedAttendeeIsAlsoAWalkIn()
    {
        var pedro = Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn };

        var result = Qualification.Classify([pedro], NoList, [], Verdicts((pedro, "do_not_invite")));

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBe(ExclusionReason.DoNotInvite);
    }

    #endregion

    #region Las decisiones manuales

    [Fact]
    public void Classify_ReturnsDoNotInviteOverruled_WhenABlacklistedAttendeeIsManuallyIncluded()
    {
        // El test del orden: la lista negra va por encima de la persona (spec 01 §3).
        var ana = Invitee("ana@agencia.com");
        var include = Override("ana@agencia.com", AttendeeOverrideDecision.Included);

        var result = Qualification.Classify([ana], NoList, [include], Verdicts((ana, "do_not_invite")));

        var classified = result.Attendees.ShouldHaveSingleItem();
        classified.Qualified.ShouldBeFalse();
        classified.ExclusionReason.ShouldBe(ExclusionReason.DoNotInvite);
        classified.OverruledReason.ShouldBe(ExclusionReason.DoNotInvite);
        classified.Override.ShouldBe(include);
    }

    [Fact]
    public void Classify_ReturnsQualified_WhenAnIncludedContactIsMissingFromTheCrm()
    {
        var ana = Invitee("ana@agencia.com");

        var result = Qualification.Classify(
            [ana], NoList, [Override("ana@agencia.com", AttendeeOverrideDecision.Included)], Verdicts());

        var classified = result.Attendees.ShouldHaveSingleItem();
        classified.Qualified.ShouldBeTrue();
        classified.ExclusionReason.ShouldBeNull();
        classified.OverruledReason.ShouldBeNull();
    }

    [Theory]
    [InlineData("ana@agencia.com", "otra@agencia.com", ExclusionReason.NotAwardsMember)]
    [InlineData("ana@sorteo360.local", "otra@agencia.com", ExclusionReason.NotAwardsMember)]
    public void Classify_ReturnsQualifiedWithTheReasonItBeat_WhenAnIncludeBeatsAnOverridableReason(
        string email, string member, string overruled)
    {
        var result = Classify(
            [Invitee(email)],
            ListOf(member),
            Override(email, AttendeeOverrideDecision.Included));

        var classified = result.Attendees.ShouldHaveSingleItem();
        classified.Qualified.ShouldBeTrue();
        classified.OverruledReason.ShouldBe(overruled);
    }

    [Fact]
    public void Classify_ReturnsQualifiedWithNothingOverruled_WhenTheIncludedAttendeeQualifiesAnyway()
    {
        var result = Classify([Invitee("ana@agencia.com")], NoList, Override("ana@agencia.com", AttendeeOverrideDecision.Included));

        var classified = result.Attendees.ShouldHaveSingleItem();
        classified.Qualified.ShouldBeTrue();
        classified.OverruledReason.ShouldBeNull();
        classified.Override.ShouldNotBeNull().Decision.ShouldBe(AttendeeOverrideDecision.Included);
    }

    [Fact]
    public void Classify_ReturnsManuallyExcludedWithNothingOverruled_WhenAQualifierIsExcluded()
    {
        var result = Classify([Invitee("ana@agencia.com")], NoList, Override("ana@agencia.com", AttendeeOverrideDecision.Excluded));

        var classified = result.Attendees.ShouldHaveSingleItem();
        classified.ExclusionReason.ShouldBe(ExclusionReason.ManuallyExcluded);
        classified.OverruledReason.ShouldBeNull();
    }

    [Fact]
    public void Classify_ReturnsManuallyExcluded_WhenAnExcludedAttendeeAlsoHasADotDoAddress()
    {
        var result = Classify([Invitee("ana@sorteo360.local")], NoList, Override("ana@sorteo360.local", AttendeeOverrideDecision.Excluded));

        result.Attendees.ShouldHaveSingleItem().ExclusionReason.ShouldBe(ExclusionReason.ManuallyExcluded);
    }

    [Fact]
    public void Classify_ReturnsTheReasonRankedAbove_WhenAnExcludeIsShadowed()
    {
        var pedro = Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn };
        var ana = Invitee("ana@agencia.com");

        var result = Qualification.Classify(
            [pedro, ana],
            NoList,
            [
                Override("pedro@agencia.com", AttendeeOverrideDecision.Excluded),
                Override("ana@agencia.com", AttendeeOverrideDecision.Excluded),
            ],
            Verdicts((pedro, null), (ana, "do_not_invite")));

        result.Attendees.Select(a => (a.ExclusionReason, a.OverruledReason)).ShouldBe(
        [
            (ExclusionReason.ManuallyExcluded, (string?)null),
            (ExclusionReason.DoNotInvite, null),
        ]);
        result.Attendees.ShouldAllBe(a => a.Override != null);
    }

    [Fact]
    public void Classify_ReturnsNoOverride_WhenTheOverrideNamesSomebodyElse()
    {
        var result = Classify([Invitee("ana@sorteo360.local")], NoList, Override("otra@sorteo360.local", AttendeeOverrideDecision.Included));

        var classified = result.Attendees.ShouldHaveSingleItem();
        classified.Qualified.ShouldBeTrue();
        classified.ExclusionReason.ShouldBeNull();
        classified.Override.ShouldBeNull();
        classified.OverruledReason.ShouldBeNull();
    }

    [Fact]
    public void Classify_ReturnsCountsThatAddUpToTheDoor_WhenOverridesAndTheCrmAreInPlay()
    {
        Attendee[] door =
        [
            Invitee("ana@agencia.com"),
            Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn },
            Invitee("luis@agencia.com") with { ContactId = null },
            Invitee("vetada@agencia.com"),
            Invitee("incluida@sorteo360.local"),
            Invitee("excluida@agencia.com"),
            Invitee("vetada.incluida@agencia.com"),
            Invitee("borrada@agencia.com"),
        ];
        var verdicts = Verdicts(
            (door[0], null), (door[1], null), (door[3], "do_not_invite"), (door[4], null),
            (door[5], null), (door[6], "do_not_invite"));

        var result = Qualification.Classify(
            door,
            NoList,
            [
                Override("incluida@sorteo360.local", AttendeeOverrideDecision.Included),
                Override("excluida@agencia.com", AttendeeOverrideDecision.Excluded),
                Override("vetada.incluida@agencia.com", AttendeeOverrideDecision.Included),
            ],
            verdicts);

        result.CheckedIn.ShouldBe(8);
        result.Qualified.ShouldBe(5);
        (result.Qualified + result.NotQualified).ShouldBe(result.CheckedIn);
        result.Attendees.Select(a => a.ExclusionReason).ShouldBe(
        [
            null,
            null,
            null,
            ExclusionReason.DoNotInvite,
            null,
            ExclusionReason.ManuallyExcluded,
            ExclusionReason.DoNotInvite,
            null,
        ]);
    }

    #endregion

    /// <summary>
    /// Clasifica con el CRM contestando por todos los de la puerta que tienen contacto, y sin
    /// vetar a nadie: lo que se prueba es el resto del pipeline.
    /// </summary>
    private static QualificationResult Classify(
        IReadOnlyList<Attendee> door,
        Maybe<AwardsList> awardsList,
        params AttendeeOverride[] overrides) =>
        Qualification.Classify(
            door,
            awardsList,
            overrides,
            Verdicts(door.Where(a => a.ContactId is not null).Select(a => (a, (string?)null)).ToArray()));

    /// <summary>La respuesta del CRM: por qué no se puede invitar a cada uno (null si se puede), y nadie más.</summary>
    private static Maybe<IReadOnlyDictionary<Guid, string?>> Verdicts(params (Attendee Attendee, string? Reason)[] verdicts) =>
        Maybe.From<IReadOnlyDictionary<Guid, string?>>(
            verdicts.ToDictionary(v => v.Attendee.ContactId!.Value, v => v.Reason));

    private static AttendeeOverride Override(string email, string decision) => new(
        email, decision, Note: null, DecidedById: Guid.NewGuid(), DecidedAt: new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero));

    private static Attendee Invitee(string email) => new(
        EmailNormalized: email,
        FirstName: "Nombre",
        LastName: "Apellido",
        Company: "Agencia",
        ContactId: Guid.NewGuid(),
        Origin: AttendeeOrigin.Invited,
        CheckedInAt: new DateTimeOffset(2026, 10, 1, 19, 0, 0, TimeSpan.Zero));

    private static Maybe<AwardsList> ListOf(params string[] members) =>
        Maybe.From(new AwardsList(Guid.NewGuid(), new HashSet<string>(members, StringComparer.Ordinal)));
}

using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// La fila congelada de un asistente (spec 01 §4.1): lo que el pipeline dijo de él, con la decisión
/// manual copiada entera para que una sola lectura de la tabla responda al acta sin un join.
/// </summary>
public sealed class RaffleAttendeeTests
{
    private static readonly Guid DeciderId = Guid.NewGuid();
    private static readonly DateTimeOffset CheckedInAt = new(2026, 10, 4, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DecidedAt = new(2026, 10, 4, 19, 30, 0, TimeSpan.Zero);

    [Fact]
    public void From_ReturnsAQualifiedRowWithNoDecision_WhenTheAttendeeQualified()
    {
        var ana = Invitee("ana@agencia.com");

        var row = RaffleAttendee.From(Classify(ana));

        row.IdentityKey.ShouldBe("ana@agencia.com");
        row.ContactId.ShouldBe(ana.ContactId);
        row.FirstName.ShouldBe("Ana");
        row.LastName.ShouldBe("Pérez");
        row.Company.ShouldBe("Agencia");
        row.CheckedInAt.ShouldBe(CheckedInAt);
        row.Qualified.ShouldBeTrue();
        row.ExclusionReason.ShouldBeNull();
        row.ManualDecision.ShouldBeNull();
        row.ManualDecidedById.ShouldBeNull();
        row.ManualDecidedAt.ShouldBeNull();
        row.ManualNote.ShouldBeNull();
        row.OverruledReason.ShouldBeNull();
    }

    [Fact]
    public void From_ReturnsAQualifiedRow_WhenTheAddressIsDominican()
    {
        var row = RaffleAttendee.From(Classify(Invitee("ana@sorteo360.local")));

        row.Qualified.ShouldBeTrue();
        row.ExclusionReason.ShouldBeNull();
        row.ManualDecision.ShouldBeNull();
        row.OverruledReason.ShouldBeNull();
    }

    [Fact]
    public void From_CopiesTheWholeDecisionAndWhatItBeat_WhenAnIncludeWon()
    {
        var ana = Invitee("ana@sorteo360.local");
        var include = Override("ana@sorteo360.local", AttendeeOverrideDecision.Included, "Invitada especial");
        var awards = Maybe.From(new AwardsList(Guid.NewGuid(), new HashSet<string>(["otra@agencia.com"], StringComparer.Ordinal)));
        var classified = Qualification.Classify(
            [ana],
            awards,
            [include],
            Maybe.From<IReadOnlyDictionary<Guid, string?>>(new Dictionary<Guid, string?> { [ana.ContactId!.Value] = null }))
            .Attendees[0];

        var row = RaffleAttendee.From(classified);

        row.Qualified.ShouldBeTrue();
        row.ExclusionReason.ShouldBeNull();
        row.ManualDecision.ShouldBe(AttendeeOverrideDecision.Included);
        row.ManualDecidedById.ShouldBe(DeciderId);
        row.ManualDecidedAt.ShouldBe(DecidedAt);
        row.ManualNote.ShouldBe("Invitada especial");
        row.OverruledReason.ShouldBe(ExclusionReason.NotAwardsMember);
    }

    [Fact]
    public void From_CopiesTheDecisionAndWhatBeatIt_WhenAnIncludeLostToTheBlacklist()
    {
        var vetado = Invitee("vetado@agencia.com");
        var include = Override("vetado@agencia.com", AttendeeOverrideDecision.Included, "Lo pidió la dirección");
        var classified = Qualification.Classify(
            [vetado],
            Maybe<AwardsList>.None,
            [include],
            Maybe.From<IReadOnlyDictionary<Guid, string?>>(new Dictionary<Guid, string?> { [vetado.ContactId!.Value] = "do_not_invite" }))
            .Attendees[0];

        var row = RaffleAttendee.From(classified);

        row.Qualified.ShouldBeFalse();
        row.ExclusionReason.ShouldBe(ExclusionReason.DoNotInvite);
        row.ManualDecision.ShouldBe(AttendeeOverrideDecision.Included);
        row.ManualNote.ShouldBe("Lo pidió la dirección");
        row.OverruledReason.ShouldBe(ExclusionReason.DoNotInvite);
    }

    [Fact]
    public void From_CopiesTheDecisionWithNoOverruledReason_WhenAnExcludeWon()
    {
        var exclude = Override("ana@agencia.com", AttendeeOverrideDecision.Excluded, null);

        var row = RaffleAttendee.From(Classify(Invitee("ana@agencia.com"), exclude));

        row.Qualified.ShouldBeFalse();
        row.ExclusionReason.ShouldBe(ExclusionReason.ManuallyExcluded);
        row.ManualDecision.ShouldBe(AttendeeOverrideDecision.Excluded);
        row.ManualDecidedById.ShouldBe(DeciderId);
        row.ManualDecidedAt.ShouldBe(DecidedAt);
        row.ManualNote.ShouldBeNull();
        row.OverruledReason.ShouldBeNull();
    }

    [Fact]
    public void From_ReturnsAQualifiedRowWithNoContact_WhenTheAttendeeCameFromRosterWithoutCrm()
    {
        var row = RaffleAttendee.From(Classify(Invitee("luis@agencia.com") with { ContactId = null }));

        row.ContactId.ShouldBeNull();
        row.Qualified.ShouldBeTrue();
        row.ExclusionReason.ShouldBeNull();
    }

    private static QualifiedAttendee Classify(Attendee attendee, params AttendeeOverride[] overrides) =>
        Qualification.Classify(
            [attendee],
            Maybe<AwardsList>.None,
            overrides,
            Maybe.From<IReadOnlyDictionary<Guid, string?>>(attendee.ContactId is { } id
                ? new Dictionary<Guid, string?> { [id] = null }
                : new Dictionary<Guid, string?>()))
            .Attendees[0];

    private static AttendeeOverride Override(string email, string decision, string? note) =>
        new(email, decision, note, DeciderId, DecidedAt);

    private static Attendee Invitee(string email) => new(
        IdentityKey: email,
        FirstName: "Ana",
        LastName: "Pérez",
        Company: "Agencia",
        ContactId: Guid.NewGuid(),
        Origin: AttendeeOrigin.Invited,
        CheckedInAt: CheckedInAt);
}

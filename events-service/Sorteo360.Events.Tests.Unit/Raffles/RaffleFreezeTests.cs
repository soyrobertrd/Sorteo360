using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// Lo que el arranque decide bajo el bloqueo (spec 01 §4, §9): el pipeline una vez más, una fila
/// por cada persona en la puerta, y ningún bombo vacío.
/// </summary>
public sealed class RaffleFreezeTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 20, 0, 0, TimeSpan.Zero);
    private static readonly Raffle Draft = Raffle.CreateDraft(Guid.NewGuid(), UserId, Now.AddDays(-1));

    [Fact]
    public void Create_ReturnsTheStartedRaffleAndOneRowPerPersonAtTheDoor_WhenSomebodyQualifies()
    {
        var batchId = Guid.NewGuid();
        var door = new[] { Invitee("ana@agencia.com"), Invitee("luis@sorteo360.local"), Invitee("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn } };
        var awards = Maybe.From(new AwardsList(batchId, new HashSet<string> { "ana@agencia.com", "luis@sorteo360.local" }));

        var result = RaffleFreeze.Create(Draft, door, awards, [], AllClear(door), UserId, Now);

        var freeze = result.Value;
        freeze.Started.State.ShouldBe(RaffleState.InProgress);
        freeze.Started.AwardsBatchId.ShouldBe(batchId);
        freeze.Started.StartedAt.ShouldBe(Now);
        freeze.Attendees.Select(a => a.EmailNormalized).ShouldBe(door.Select(a => a.EmailNormalized), ignoreOrder: true);
        freeze.Attendees.Count(a => a.Qualified).ShouldBe(2);
    }

    [Fact]
    public void Create_AppliesTheOverridesReadUnderTheLock_WhenThereAreSome()
    {
        var door = new[] { Invitee("ana@sorteo360.local") };
        var include = new AttendeeOverride("ana@sorteo360.local", AttendeeOverrideDecision.Included, "Invitada", UserId, Now);

        var result = RaffleFreeze.Create(Draft, door, Maybe<AwardsList>.None, [include], AllClear(door), UserId, Now);

        var row = result.Value.Attendees.ShouldHaveSingleItem();
        row.Qualified.ShouldBeTrue();
        row.ManualNote.ShouldBe("Invitada");
    }

    [Fact]
    public void Create_ReturnsRaffleNoParticipants_WhenNobodyIsAtTheDoor()
    {
        var result = RaffleFreeze.Create(Draft, [], Maybe<AwardsList>.None, [], AllClear([]), UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_NO_PARTICIPANTS");
    }

    [Fact]
    public void Create_ReturnsRaffleNoParticipants_WhenEveryoneIsExcluded()
    {
        var door = new[] { Invitee("ana@sorteo360.local"), Invitee("luis@turismo.com.do") };
        AttendeeOverride[] exclusions =
        [
            new("ana@sorteo360.local", AttendeeOverrideDecision.Excluded, "Sin premio", UserId, Now),
            new("luis@turismo.com.do", AttendeeOverrideDecision.Excluded, "Sin premio", UserId, Now),
        ];

        var result = RaffleFreeze.Create(Draft, door, Maybe<AwardsList>.None, exclusions, AllClear(door), UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_NO_PARTICIPANTS");
    }

    [Fact]
    public void Create_ReturnsRaffleNoParticipants_WhenTheAwardsListMatchesNobody()
    {
        var door = new[] { Invitee("ana@agencia.com") };
        var awards = Maybe.From(new AwardsList(Guid.NewGuid(), new HashSet<string> { "otro@agencia.com" }));

        var result = RaffleFreeze.Create(Draft, door, awards, [], AllClear(door), UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_NO_PARTICIPANTS");
    }

    [Fact]
    public void Create_ReturnsRaffleNotDraft_WhenAnotherStartGotThereFirst()
    {
        var door = new[] { Invitee("ana@agencia.com") };
        var started = Draft with { State = RaffleState.InProgress };

        var result = RaffleFreeze.Create(started, door, Maybe<AwardsList>.None, [], AllClear(door), UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_NOT_DRAFT");
    }

    private static IReadOnlyDictionary<Guid, string?> AllClear(IEnumerable<Attendee> door) =>
        door.Where(a => a.ContactId is not null).ToDictionary(a => a.ContactId!.Value, _ => (string?)null);

    private static Attendee Invitee(string email) => new(
        EmailNormalized: email,
        FirstName: "Nombre",
        LastName: "Apellido",
        Company: "Agencia",
        ContactId: Guid.NewGuid(),
        Origin: AttendeeOrigin.Invited,
        CheckedInAt: Now);
}

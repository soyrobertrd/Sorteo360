using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class NotarialRecordFactsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OperatorIds_ReturnsEachManualDeciderOnce_WhenSeveralDecisionsShareOne()
    {
        var ana = Guid.NewGuid();
        var luis = Guid.NewGuid();
        var facts = Facts(Decided(ana), Decided(luis), Decided(ana), Decided(null));

        facts.OperatorIds().ShouldBe([ana, luis], ignoreOrder: true);
    }

    [Fact]
    public void OperatorIds_ReturnsNobodyElse_WhenTheRaffleWasStartedAndCompleted()
    {
        // El acta nombra a quien tomó una decisión manual (spec 03 §4.4 paso 4), y nadie más: cada
        // id es una llamada al realm con la fila del sorteo bloqueada.
        var raffle = Raffle.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Now).Start(null, Guid.NewGuid(), Now).Value
            with { CompletedById = Guid.NewGuid(), CompletedAt = Now };
        var facts = Facts(Decided(null)) with { Raffle = new RaffleWithRounds(raffle, []) };

        facts.OperatorIds().ShouldBeEmpty();
    }

    private static NotarialRecordFacts Facts(params RaffleAttendee[] attendees) => new(
        new RaffleWithRounds(Raffle.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Now), []),
        attendees,
        new NotarialRecordEvent("Roadshow", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), []));

    private static RaffleAttendee Decided(Guid? decidedById) => new(
        Guid.NewGuid(), $"{Guid.NewGuid():N}@agencia.com", null, "Ana", "Pérez", null, Now, true, null,
        decidedById is null ? null : "included", decidedById, decidedById is null ? null : Now, null, null);
}

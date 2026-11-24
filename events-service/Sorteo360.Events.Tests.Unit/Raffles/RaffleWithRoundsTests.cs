using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RaffleWithRoundsTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData(RaffleState.Draft)]
    [InlineData(RaffleState.InProgress)]
    public void EnsureCanChangeSetup_ReturnsSuccess_WhenNotCompleted(string state)
    {
        var raffle = Aggregate(state);

        raffle.EnsureCanChangeSetup().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EnsureCanChangeSetup_ReturnsRaffleCompleted_WhenCompleted()
    {
        var raffle = Aggregate(RaffleState.Completed);

        raffle.EnsureCanChangeSetup().Error.Code.ShouldBe("RAFFLE_COMPLETED");
    }

    [Fact]
    public void FindRound_ReturnsTheRound_WhenItIsOfThisRaffle()
    {
        var round = Round(1);
        var raffle = Aggregate(RaffleState.Draft, round);

        raffle.FindRound(round.Id).Value.Id.ShouldBe(round.Id);
    }

    [Fact]
    public void FindRound_ReturnsRoundNotFound_WhenItIsNotOfThisRaffle()
    {
        var raffle = Aggregate(RaffleState.Draft, Round(1));

        raffle.FindRound(Guid.NewGuid()).Error.Code.ShouldBe("ROUND_NOT_FOUND");
    }

    [Fact]
    public void FindPrize_ReturnsThePrize_WhenItIsInAnyRound()
    {
        var first = Round(1);
        var second = Round(2);
        var prize = PrizeIn(second.Id, Now);
        var raffle = Aggregate(RaffleState.Draft, first, second with { Prizes = [prize] });

        raffle.FindPrize(prize.Id).Value.Id.ShouldBe(prize.Id);
    }

    [Fact]
    public void FindPrize_ReturnsPrizeNotFound_WhenItIsNotOfThisRaffle()
    {
        var round = Round(1);
        var raffle = Aggregate(RaffleState.Draft, round with { Prizes = [PrizeIn(round.Id, Now)] });

        raffle.FindPrize(Guid.NewGuid()).Error.Code.ShouldBe("PRIZE_NOT_FOUND");
    }

    [Fact]
    public void EnsureCanMovePrize_ReturnsThePrize_WhenBothAreOfThisRaffle()
    {
        var source = Round(1);
        var target = Round(2);
        var prize = PrizeIn(source.Id, Now);
        var raffle = Aggregate(RaffleState.Draft, source with { Prizes = [prize] }, target);

        raffle.EnsureCanMovePrize(prize.Id, target.Id).Value.Id.ShouldBe(prize.Id);
    }

    [Fact]
    public void EnsureCanMovePrize_ReturnsRoundNotFound_WhenTheRoundIsNotOfThisRaffle()
    {
        var round = Round(1);
        var prize = PrizeIn(round.Id, Now);
        var raffle = Aggregate(RaffleState.Draft, round with { Prizes = [prize] });

        raffle.EnsureCanMovePrize(prize.Id, Guid.NewGuid()).Error.Code.ShouldBe("ROUND_NOT_FOUND");
    }

    [Fact]
    public void EnsureCanMovePrize_ReturnsPrizeNotFound_WhenNeitherIsOfThisRaffle()
    {
        var raffle = Aggregate(RaffleState.Draft, Round(1));

        raffle.EnsureCanMovePrize(Guid.NewGuid(), Guid.NewGuid()).Error.Code.ShouldBe("PRIZE_NOT_FOUND");
    }

    [Fact]
    public void EnsureCanDeleteRound_ReturnsSuccess_WhenTheRoundIsEmpty()
    {
        var round = Round(1);
        var raffle = Aggregate(RaffleState.Draft, round);

        raffle.EnsureCanDeleteRound(round.Id).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EnsureCanDeleteRound_ReturnsRoundNotEmpty_WhenTheRoundHoldsAPrize()
    {
        var round = Round(1);
        var raffle = Aggregate(RaffleState.Draft, round with { Prizes = [PrizeIn(round.Id, Now)] });

        raffle.EnsureCanDeleteRound(round.Id).Error.Code.ShouldBe("ROUND_NOT_EMPTY");
    }

    [Fact]
    public void EnsureCanDeleteRound_ReturnsRoundNotFound_WhenTheRoundIsNotOfThisRaffle()
    {
        var raffle = Aggregate(RaffleState.Draft, Round(1));

        raffle.EnsureCanDeleteRound(Guid.NewGuid()).Error.Code.ShouldBe("ROUND_NOT_FOUND");
    }

    [Fact]
    public void Rounds_ReturnsRoundsByNumber_WhenLoadedOutOfOrder()
    {
        var raffle = Aggregate(RaffleState.Draft, Round(3), Round(1), Round(2));

        raffle.Rounds.Select(r => r.Number).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Rounds_ReturnsPrizesByCreationThenId_WhenLoadedOutOfOrder()
    {
        var round = Round(1);
        var later = PrizeIn(round.Id, Now.AddMinutes(1));
        var sameInstantA = PrizeIn(round.Id, Now);
        var sameInstantB = PrizeIn(round.Id, Now);
        var raffle = Aggregate(RaffleState.Draft, round with { Prizes = [later, sameInstantA, sameInstantB] });

        var expected = new[] { sameInstantA, sameInstantB }.OrderBy(p => p.Id).Select(p => p.Id).Append(later.Id);
        raffle.Rounds[0].Prizes.Select(p => p.Id).ShouldBe(expected);
    }

    [Fact]
    public void NextRoundNumber_ReturnsOne_WhenThereAreNoRounds()
    {
        Aggregate(RaffleState.Draft).NextRoundNumber.ShouldBe(1);
    }

    [Fact]
    public void NextRoundNumber_ReturnsMaxPlusOne_WhenThereAreRounds()
    {
        Aggregate(RaffleState.Draft, Round(1), Round(2)).NextRoundNumber.ShouldBe(3);
    }

    private static RaffleWithRounds Aggregate(string state, params RaffleRound[] rounds) => new(
        Raffle.CreateDraft(Guid.NewGuid(), UserId, Now) with { State = state },
        rounds);

    internal static RaffleRound Round(int number) => new(
        id: Guid.NewGuid(),
        raffleId: Guid.NewGuid(),
        number: number,
        name: $"Ronda {number}",
        description: null,
        prizes: [],
        createdAt: Now,
        updatedAt: Now,
        createdById: UserId,
        updatedById: UserId);

    internal static Prize PrizeIn(Guid roundId, DateTimeOffset createdAt) => new(
        id: Guid.NewGuid(),
        roundId: roundId,
        name: "Bolsa",
        description: null,
        value: null,
        createdAt: createdAt,
        updatedAt: createdAt,
        createdById: UserId,
        updatedById: UserId);
}

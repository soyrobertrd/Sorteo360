using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;
using Shouldly;
using static Sorteo360.Events.Tests.Unit.Raffles.DrawFixtures;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>Lo que el sorteo de un premio decide bajo el bloqueo (spec 02 §2, §2.1, §7, §7.1).</summary>
public sealed class PrizeDrawingTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Raffle InProgress =
        Raffle.CreateDraft(Guid.NewGuid(), UserId, Now.AddDays(-1)) with { State = RaffleState.InProgress };
    private static readonly Prize ThePrize =
        new(Guid.NewGuid(), Guid.NewGuid(), "Botella de ron", null, null, Now, Now, UserId, UserId);
    private static readonly Prize EarlierPrize =
        new(Guid.NewGuid(), ThePrize.RoundId, "Cena para dos", null, null, Now, Now, UserId, UserId);

    [Theory]
    [InlineData(RaffleState.Draft, "RAFFLE_NOT_IN_PROGRESS")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public void Draw_ReturnsTheStateError_WhenTheRaffleIsNotInProgress(string state, string code)
    {
        var result = Draw(InProgress with { State = state }, [Attendee("María")], []);

        result.Error.Code.ShouldBe(code);
    }

    [Fact]
    public void Draw_ReturnsPrizeAlreadyDrawnCarryingTheLiveDraw_WhenThePrizeHasOne()
    {
        var maria = Attendee("María", company: "Hotel Sol");
        var live = LiveDraw(ThePrize.Id, maria);

        var result = Draw(InProgress, [maria, Attendee("Juan")], [live]);

        var error = result.Error.ShouldBeOfType<PrizeAlreadyDrawnError>();
        error.Code.ShouldBe("PRIZE_ALREADY_DRAWN");
        var carried = error.ProblemExtensions["draw"].ShouldBeOfType<PrizeDrawSummary>();
        carried.Id.ShouldBe(live.Id);
        carried.WinnerName.ShouldBe(live.WinnerName);
        carried.Void.ShouldBeNull();
    }

    [Fact]
    public void Draw_ReturnsPoolExhaustedWithTheCounts_WhenNobodyIsLeft()
    {
        var maria = Attendee("María");
        var juan = Attendee("Juan");
        var draws = new[] { LiveDraw(Guid.NewGuid(), maria), VoidedDraw(ThePrize.Id, juan, PrizeDrawVoidCode.Declined) };

        var result = Draw(InProgress, [maria, juan, Attendee("Pedro", qualified: false)], draws);

        var error = result.Error.ShouldBeOfType<PoolExhaustedError>();
        error.ProblemExtensions["participants"].ShouldBe(2);
        error.ProblemExtensions["excludedByVoid"].ShouldBe(1);
        error.ProblemExtensions["excludedByPerson"].ShouldBe(1);
        error.ProblemExtensions["excludedByCompany"].ShouldBe(0);
    }

    [Fact]
    public void Draw_ReturnsTheWinnerAtThePickedIndexWithTheirSnapshot_WhenThePoolHasMembers()
    {
        var attendees = new[] { Attendee("María", company: "Hotel Sol"), Attendee("Juan"), Attendee("Ana") };
        var ordered = attendees.OrderBy(a => a.Id).ToList();
        int? askedFor = null;
        const string code = "RD-ABCD-1234";

        var result = PrizeDrawing.Draw(InProgress, ThePrize, attendees, [], n => { askedFor = n; return 2; }, UserId, Now, code);

        var draw = result.Value;
        var winner = ordered[2];
        askedFor.ShouldBe(3);
        draw.PrizeId.ShouldBe(ThePrize.Id);
        draw.WinnerAttendeeId.ShouldBe(winner.Id);
        draw.WinnerName.ShouldBe($"{winner.FirstName} {winner.LastName}");
        draw.WinnerCompany.ShouldBe(winner.Company);
        draw.WinnerEmail.ShouldBe(winner.EmailNormalized);
        draw.EligiblePoolSize.ShouldBe(3);
        draw.DrawnAt.ShouldBe(Now);
        draw.DrawnById.ShouldBe(UserId);
        draw.RedemptionCode.ShouldBe(code);
        draw.IsLive.ShouldBeTrue();
    }

    [Fact]
    public void Draw_ReturnsATrimmedWinnerName_WhenTheLastNameIsBlank()
    {
        var maria = Attendee("María") with { LastName = "" };

        var result = Draw(InProgress, [maria], []);

        result.Value.WinnerName.ShouldBe("María");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Draw_ReturnsADrawCarryingTheRaffleRules_WhenThePoolHasMembers(bool person, bool company)
    {
        var raffle = InProgress with { OnePrizePerPerson = person, OnePrizePerCompany = company };

        var result = Draw(raffle, [Attendee("María")], []);

        // Un premio por persona rige siempre: el sorteo lo deja escrito aunque el sorteo diga que no.
        result.Value.OnePrizePerPerson.ShouldBeTrue();
        result.Value.OnePrizePerCompany.ShouldBe(company);
    }

    [Fact]
    public void Draw_NeverReturnsAnEarlierWinner_EvenWithOnePrizePerPersonTurnedOff()
    {
        var maria = Attendee("María");
        var first = DrawEarlier(InProgress with { OnePrizePerPerson = true }, [maria]);

        var whileOff = Draw(InProgress with { OnePrizePerPerson = false }, [maria], [first]);
        var backOn = Draw(InProgress with { OnePrizePerPerson = true }, [maria], [first]);

        whileOff.Error.Code.ShouldBe("RAFFLE_POOL_EXHAUSTED");
        backOn.Error.Code.ShouldBe("RAFFLE_POOL_EXHAUSTED");
    }

    [Fact]
    public void Draw_ReturnsTheEarlierWinnersColleagueInThePoolAgain_WhenOnePrizePerCompanyIsTurnedOffAfterTheDraw()
    {
        var maria = Attendee("María", company: "Hotel Sol");
        var juan = Attendee("Juan", company: "Hotel Sol");
        var companyOn = InProgress with { OnePrizePerCompany = true };
        var first = DrawEarlier(companyOn, [maria]);

        var whileOff = Draw(InProgress with { OnePrizePerCompany = false }, [maria, juan], [first]);
        var backOn = Draw(companyOn, [maria, juan], [first]);

        whileOff.Value.WinnerAttendeeId.ShouldBe(juan.Id);
        whileOff.Value.OnePrizePerCompany.ShouldBeFalse();
        first.OnePrizePerCompany.ShouldBeTrue();
        backOn.Error.Code.ShouldBe("RAFFLE_POOL_EXHAUSTED");
    }

    private static Result<PrizeDraw, BaseError> Draw(
        Raffle raffle, IReadOnlyList<RaffleAttendee> attendees, IReadOnlyList<PrizeDraw> draws) =>
        PrizeDrawing.Draw(raffle, ThePrize, attendees, draws, _ => 0, UserId, Now, RedemptionCode.Generate());

    private static PrizeDraw DrawEarlier(Raffle raffle, IReadOnlyList<RaffleAttendee> attendees) =>
        PrizeDrawing.Draw(raffle, EarlierPrize, attendees, [], _ => 0, UserId, Now, RedemptionCode.Generate()).Value;
}

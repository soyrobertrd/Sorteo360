using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;
using Shouldly;
using static Sorteo360.Events.Tests.Unit.Raffles.DrawFixtures;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>Lo que la anulación decide bajo el bloqueo (spec 02 §3, §7).</summary>
public sealed class PrizeVoidingTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Raffle InProgress =
        Raffle.CreateDraft(Guid.NewGuid(), UserId, Now.AddDays(-1)) with { State = RaffleState.InProgress };
    private static readonly Prize ThePrize =
        new(Guid.NewGuid(), Guid.NewGuid(), "Botella de ron", null, null, Now, Now, UserId, UserId);

    #region Void

    [Theory]
    [InlineData(RaffleState.Draft, "RAFFLE_NOT_IN_PROGRESS")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public void Void_ReturnsTheStateError_WhenTheRaffleIsNotInProgress(string state, string code)
    {
        var draws = new[] { LiveDraw(ThePrize.Id, Attendee("María")) };

        var result = Void(InProgress with { State = state }, draws);

        result.Error.Code.ShouldBe(code);
    }

    [Fact]
    public void Void_ReturnsPrizeNotDrawn_WhenThePrizeHasNoDraws()
    {
        var draws = new[] { LiveDraw(Guid.NewGuid(), Attendee("María")) };

        var result = Void(InProgress, draws);

        result.Error.Code.ShouldBe("PRIZE_NOT_DRAWN");
    }

    [Fact]
    public void Void_ReturnsPrizeNotDrawn_WhenThePrizeHasOnlyVoidedDraws()
    {
        var draws = new[] { VoidedDraw(ThePrize.Id, Attendee("María"), PrizeDrawVoidCode.Absent) };

        var result = Void(InProgress, draws);

        result.Error.Code.ShouldBe("PRIZE_NOT_DRAWN");
    }

    [Fact]
    public void Void_ReturnsTheLiveDrawWithTheFourColumnsSet_WhenThePrizeIsDrawn()
    {
        var voided = VoidedDraw(ThePrize.Id, Attendee("Juan"), PrizeDrawVoidCode.Declined);
        var live = LiveDraw(ThePrize.Id, Attendee("María"));
        var voidedById = Guid.NewGuid();
        var at = Now.AddMinutes(5);

        var result = PrizeVoiding.Void(
            InProgress, ThePrize, [voided, live], PrizeDrawVoidCode.Other, "Se fue antes", voidedById, at);

        var draw = result.Value;
        draw.Id.ShouldBe(live.Id);
        draw.RedemptionCode.ShouldBe(live.RedemptionCode);
        draw.WinnerAttendeeId.ShouldBe(live.WinnerAttendeeId);
        draw.VoidCode.ShouldBe(PrizeDrawVoidCode.Other);
        draw.VoidNote.ShouldBe("Se fue antes");
        draw.VoidedAt.ShouldBe(at);
        draw.VoidedById.ShouldBe(voidedById);
        draw.IsLive.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PrizeDrawVoidCode.Absent)]
    [InlineData(PrizeDrawVoidCode.Declined)]
    [InlineData(PrizeDrawVoidCode.Ineligible)]
    [InlineData(PrizeDrawVoidCode.Other)]
    public void Void_ReturnsADrawThatTakesTheWinnerOutOfTheRaffle_WhenVoidedWithAnyCode(string code)
    {
        var maria = Attendee("María");
        var juan = Attendee("Juan");

        var voided = PrizeVoiding.Void(
            InProgress, ThePrize, [LiveDraw(ThePrize.Id, maria)], code, null, UserId, Now).Value;

        var pool = EligiblePool.For([maria, juan], [voided], onePrizePerCompany: false);
        pool.Members.ShouldBe([juan]);
    }

    [Fact]
    public void Void_ReturnsPrizeAlreadyRedeemed_WhenTheLiveDrawHasALiveHandover()
    {
        var draw = LiveDraw(ThePrize.Id, Attendee("María"));

        var result = Void(InProgress, [draw.With(LiveHandover(draw))]);

        result.Error.Code.ShouldBe("PRIZE_ALREADY_REDEEMED");
    }

    [Fact]
    public void Void_ReturnsTheVoidedDraw_WhenTheHandoverWasReversed()
    {
        var draw = LiveDraw(ThePrize.Id, Attendee("María"));

        var result = Void(InProgress, [draw.With(ReversedHandover(draw))]);

        result.Value.IsLive.ShouldBeFalse();
    }

    #endregion

    private static Result<PrizeDraw, BaseError> Void(Raffle raffle, IReadOnlyList<PrizeDraw> draws) =>
        PrizeVoiding.Void(raffle, ThePrize, draws, PrizeDrawVoidCode.Absent, null, UserId, Now);
}

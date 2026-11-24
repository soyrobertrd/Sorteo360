using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;
using Shouldly;
using static Sorteo360.Events.Tests.Unit.Raffles.DrawFixtures;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>Lo que la entrega y su reversión deciden bajo el bloqueo (spec 03 §3.4).</summary>
public sealed class PrizeHandingOverTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Raffle InProgress =
        Raffle.CreateDraft(Guid.NewGuid(), UserId, Now.AddDays(-1)) with { State = RaffleState.InProgress };
    private static readonly Prize ThePrize =
        new(Guid.NewGuid(), Guid.NewGuid(), "Botella de ron", null, null, Now, Now, UserId, UserId);
    private static readonly PrizeReceiver Receiver = new("Ana Gómez", "001-7654321-0", IsWinner: false);

    #region HandOver

    [Fact]
    public void HandOver_ReturnsRaffleNotInProgress_WhenTheRaffleIsADraft()
    {
        var result = HandOver(InProgress with { State = RaffleState.Draft }, []);

        result.Error.Code.ShouldBe("RAFFLE_NOT_IN_PROGRESS");
    }

    [Fact]
    public void HandOver_ReturnsPrizeNotDrawn_WhenThePrizeHasNoDraw()
    {
        var result = HandOver(InProgress, [LiveDraw(Guid.NewGuid(), Attendee("María"))]);

        result.Error.Code.ShouldBe("PRIZE_NOT_DRAWN");
    }

    [Fact]
    public void HandOver_ReturnsPrizeNotDrawn_WhenThePrizeHasOnlyVoidedDraws()
    {
        var result = HandOver(InProgress, [VoidedDraw(ThePrize.Id, Attendee("María"), PrizeDrawVoidCode.Absent)]);

        result.Error.Code.ShouldBe("PRIZE_NOT_DRAWN");
    }

    [Fact]
    public void HandOver_ReturnsPrizeAlreadyRedeemed_WhenTheLiveDrawHasALiveHandover()
    {
        var draw = LiveDraw(ThePrize.Id, Attendee("María"));

        var result = HandOver(InProgress, [draw.With(LiveHandover(draw))]);

        result.Error.Code.ShouldBe("PRIZE_ALREADY_REDEEMED");
    }

    [Theory]
    [InlineData(RaffleState.InProgress)]
    [InlineData(RaffleState.Completed)]
    public void HandOver_ReturnsALiveHandoverOnTheLiveDraw_WhenThePrizeIsDrawn(string state)
    {
        var voided = VoidedDraw(ThePrize.Id, Attendee("Juan"), PrizeDrawVoidCode.Declined);
        var live = LiveDraw(ThePrize.Id, Attendee("María"));
        live = live.With(ReversedHandover(live));
        var signatureId = Guid.NewGuid();
        var at = Now.AddDays(3);

        var result = PrizeHandingOver.HandOver(
            InProgress with { State = state }, ThePrize, [voided, live], Receiver, signatureId, UserId, at);

        var handover = result.Value;
        handover.PrizeDrawId.ShouldBe(live.Id);
        handover.ReceiverName.ShouldBe("Ana Gómez");
        handover.ReceiverIdentityDocument.ShouldBe("001-7654321-0");
        handover.ReceiverIsWinner.ShouldBeFalse();
        handover.SignatureStoredFileId.ShouldBe(signatureId);
        handover.RedeemedAt.ShouldBe(at);
        handover.RedeemedById.ShouldBe(UserId);
        handover.IsLive.ShouldBeTrue();
    }

    #endregion

    #region Reverse

    [Fact]
    public void Reverse_ReturnsRaffleNotInProgress_WhenTheRaffleIsADraft()
    {
        var result = Reverse(InProgress with { State = RaffleState.Draft }, []);

        result.Error.Code.ShouldBe("RAFFLE_NOT_IN_PROGRESS");
    }

    [Fact]
    public void Reverse_ReturnsPrizeNotRedeemed_WhenThePrizeIsUndrawn()
    {
        var result = Reverse(InProgress, []);

        result.Error.Code.ShouldBe("PRIZE_NOT_REDEEMED");
    }

    [Fact]
    public void Reverse_ReturnsPrizeNotRedeemed_WhenThePrizeIsDrawn()
    {
        var draw = LiveDraw(ThePrize.Id, Attendee("María"));

        var result = Reverse(InProgress, [draw.With(ReversedHandover(draw))]);

        result.Error.Code.ShouldBe("PRIZE_NOT_REDEEMED");
    }

    [Theory]
    [InlineData(RaffleState.InProgress)]
    [InlineData(RaffleState.Completed)]
    public void Reverse_ReturnsTheLiveHandoverWithTheThreeColumnsSet_WhenThePrizeIsRedeemed(string state)
    {
        var draw = LiveDraw(ThePrize.Id, Attendee("María"));
        var live = LiveHandover(draw);
        var reversedById = Guid.NewGuid();
        var at = Now.AddDays(4);

        var result = PrizeHandingOver.Reverse(
            InProgress with { State = state }, ThePrize, [draw.With(live)], "Premio equivocado", reversedById, at);

        var reversed = result.Value;
        reversed.Id.ShouldBe(live.Id);
        reversed.SignatureStoredFileId.ShouldBe(live.SignatureStoredFileId);
        reversed.ReversedAt.ShouldBe(at);
        reversed.ReversedById.ShouldBe(reversedById);
        reversed.ReversalReason.ShouldBe("Premio equivocado");
        reversed.IsLive.ShouldBeFalse();
    }

    #endregion

    private static Result<PrizeHandover, BaseError> HandOver(Raffle raffle, IReadOnlyList<PrizeDraw> draws) =>
        PrizeHandingOver.HandOver(raffle, ThePrize, draws, Receiver, Guid.NewGuid(), UserId, Now);

    private static Result<PrizeHandover, BaseError> Reverse(Raffle raffle, IReadOnlyList<PrizeDraw> draws) =>
        PrizeHandingOver.Reverse(raffle, ThePrize, draws, "Motivo", UserId, Now);
}

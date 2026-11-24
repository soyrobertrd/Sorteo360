using Sorteo360.Events.Core.Raffles;
using Shouldly;
using static Sorteo360.Events.Tests.Unit.Raffles.DrawFixtures;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// El eje del premio contra la tabla de entregas (spec 03 §1) y el estado de cada sorteo, que no es
/// ese eje: un premio nunca está anulado y un sorteo nunca está sin sortear (spec 03 §3.4).
/// </summary>
public sealed class PrizeStateTests
{
    private static readonly Guid PrizeId = Guid.NewGuid();

    [Fact]
    public void Of_ReturnsUndrawn_WhenThePrizeHasNoDraw()
    {
        PrizeState.Of([]).ShouldBe(PrizeState.Undrawn);
    }

    [Fact]
    public void Of_ReturnsDrawnAndDrawn_WhenTheDrawIsLiveWithNoHandover()
    {
        var draw = LiveDraw(PrizeId, Attendee("María"));

        PrizeState.Of([draw]).ShouldBe(PrizeState.Drawn);
        PrizeDrawStatus.Of(draw).ShouldBe(PrizeDrawStatus.Drawn);
    }

    [Fact]
    public void Of_ReturnsRedeemedAndRedeemed_WhenTheLiveDrawHasALiveHandover()
    {
        var draw = LiveDraw(PrizeId, Attendee("María"));
        draw = draw.With(LiveHandover(draw));

        PrizeState.Of([draw]).ShouldBe(PrizeState.Redeemed);
        PrizeDrawStatus.Of(draw).ShouldBe(PrizeDrawStatus.Redeemed);
    }

    [Fact]
    public void Of_ReturnsDrawnAndDrawn_WhenTheLiveDrawHasOnlyAReversedHandover()
    {
        var draw = LiveDraw(PrizeId, Attendee("María"));
        draw = draw.With(ReversedHandover(draw));

        PrizeState.Of([draw]).ShouldBe(PrizeState.Drawn);
        PrizeDrawStatus.Of(draw).ShouldBe(PrizeDrawStatus.Drawn);
    }

    [Fact]
    public void Of_ReturnsUndrawnAndVoided_WhenTheOnlyDrawIsVoidedWithAReversedHandover()
    {
        var draw = VoidedDraw(PrizeId, Attendee("María"), PrizeDrawVoidCode.Other);
        draw = draw.With(ReversedHandover(draw));

        PrizeState.Of([draw]).ShouldBe(PrizeState.Undrawn);
        PrizeDrawStatus.Of(draw).ShouldBe(PrizeDrawStatus.Voided);
    }

    [Fact]
    public void Of_ReturnsRedeemed_WhenAVoidedDrawSitsBesideARedeemedOne()
    {
        var voided = VoidedDraw(PrizeId, Attendee("Juan"), PrizeDrawVoidCode.Absent);
        var live = LiveDraw(PrizeId, Attendee("María"));
        live = live.With(ReversedHandover(live, Now.AddDays(1)), LiveHandover(live, Now.AddDays(2)));

        PrizeState.Of([voided, live]).ShouldBe(PrizeState.Redeemed);
        live.LiveHandover.ShouldBe(live.Handovers[0]);
    }
}

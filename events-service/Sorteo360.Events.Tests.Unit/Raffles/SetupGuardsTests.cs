using Sorteo360.Events.Core.Raffles;
using Shouldly;
using static Sorteo360.Events.Tests.Unit.Raffles.DrawFixtures;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>Las guardas del armado que miran los sorteos, vivos y anulados (spec 00 §4).</summary>
public sealed class SetupGuardsTests
{
    private static readonly Prize ThePrize =
        new(Guid.NewGuid(), Guid.NewGuid(), "Botella de ron", null, null, Now, Now, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void EnsurePrizeHasNoDraws_ReturnsPrizeHasDraws_WhenThePrizeHasALiveDraw()
    {
        var result = SetupGuards.EnsurePrizeHasNoDraws(ThePrize.Id, [LiveDraw(ThePrize.Id, Attendee("María"))]);

        result.Error.Code.ShouldBe("PRIZE_HAS_DRAWS");
    }

    [Fact]
    public void EnsurePrizeHasNoDraws_ReturnsPrizeHasDraws_WhenThePrizeHasOnlyAVoidedDraw()
    {
        var draws = new[] { VoidedDraw(ThePrize.Id, Attendee("María"), PrizeDrawVoidCode.Absent) };

        var result = SetupGuards.EnsurePrizeHasNoDraws(ThePrize.Id, draws);

        result.Error.Code.ShouldBe("PRIZE_HAS_DRAWS");
    }

    [Fact]
    public void EnsurePrizeHasNoDraws_ReturnsSuccess_WhenOnlyAnotherPrizeHasDraws()
    {
        var result = SetupGuards.EnsurePrizeHasNoDraws(ThePrize.Id, [LiveDraw(Guid.NewGuid(), Attendee("María"))]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EnsureRoundHasNoDraws_ReturnsPrizeHasDrawsAboutTheRound_WhenOneOfItsPrizesHasALiveDraw()
    {
        var result = SetupGuards.EnsureRoundHasNoDraws(
            [Guid.NewGuid(), ThePrize.Id], [LiveDraw(ThePrize.Id, Attendee("María"))]);

        result.Error.Code.ShouldBe("PRIZE_HAS_DRAWS");
        result.Error.Message.ShouldContain("ronda");
    }

    [Fact]
    public void EnsureRoundHasNoDraws_ReturnsPrizeHasDraws_WhenOneOfItsPrizesHasOnlyAVoidedDraw()
    {
        var draws = new[] { VoidedDraw(ThePrize.Id, Attendee("María"), PrizeDrawVoidCode.Ineligible) };

        var result = SetupGuards.EnsureRoundHasNoDraws([ThePrize.Id], draws);

        result.Error.Code.ShouldBe("PRIZE_HAS_DRAWS");
    }

    [Fact]
    public void EnsureRoundHasNoDraws_ReturnsSuccess_WhenNoneOfItsPrizesHasDraws()
    {
        var result = SetupGuards.EnsureRoundHasNoDraws([ThePrize.Id], [LiveDraw(Guid.NewGuid(), Attendee("María"))]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EnsureCanSetSponsorLogo_ReturnsPrizeHasDraws_WhenTheSponsoredPrizeHasADraw()
    {
        var sponsored = ThePrize with { SponsorCompanyId = Guid.NewGuid() };

        var result = SetupGuards.EnsureCanSetSponsorLogo(sponsored, [LiveDraw(sponsored.Id, Attendee("María"))]);

        result.Error.Code.ShouldBe("PRIZE_HAS_DRAWS");
    }

    [Fact]
    public void EnsureCanSetSponsorLogo_ReturnsPrizeHasDraws_WhenThePrizeHasADrawAndNoSponsor()
    {
        // Lo que el premio ya no admite se dice antes que lo que le falta (spec 00 §8).
        var result = SetupGuards.EnsureCanSetSponsorLogo(ThePrize, [LiveDraw(ThePrize.Id, Attendee("María"))]);

        result.Error.Code.ShouldBe("PRIZE_HAS_DRAWS");
    }

    [Fact]
    public void EnsureCanSetSponsorLogo_ReturnsPrizeNotSponsored_WhenThePrizeHasNoSponsorCompany()
    {
        var result = SetupGuards.EnsureCanSetSponsorLogo(ThePrize, []);

        result.Error.Code.ShouldBe("PRIZE_NOT_SPONSORED");
    }

    [Fact]
    public void EnsureCanSetSponsorLogo_ReturnsSuccess_WhenThePrizeIsSponsoredAndUndrawn()
    {
        var sponsored = ThePrize with { SponsorCompanyId = Guid.NewGuid() };

        var result = SetupGuards.EnsureCanSetSponsorLogo(sponsored, [LiveDraw(Guid.NewGuid(), Attendee("María"))]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EnsureCanSetSponsorLogo_ReturnsSuccess_WhenThePrizeHasFreeTextSponsorNameAndNoCompanyId()
    {
        var sponsored = ThePrize with { SponsorCompanyId = null, SponsorName = "Banco Popular" };

        var result = SetupGuards.EnsureCanSetSponsorLogo(sponsored, []);

        result.IsSuccess.ShouldBeTrue();
    }
}

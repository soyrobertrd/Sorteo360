using Sorteo360.Events.Core.Raffles;
using Shouldly;
using static Sorteo360.Events.Tests.Unit.Raffles.DrawFixtures;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// El bombo de un premio, línea por línea contra spec 02 §1: los calificados menos quien tiene una
/// anulación en cualquier premio del sorteo, y las dos reglas, que solo miran sorteos vivos.
/// </summary>
public sealed class EligiblePoolTests
{
    private static readonly Guid PrizeP = Guid.NewGuid();
    private static readonly Guid PrizeQ = Guid.NewGuid();

    #region The three lines

    [Theory]
    [InlineData(PrizeDrawVoidCode.Absent)]
    [InlineData(PrizeDrawVoidCode.Declined)]
    [InlineData(PrizeDrawVoidCode.Ineligible)]
    [InlineData(PrizeDrawVoidCode.Other)]
    public void For_ReturnsAPoolWithoutTheVoidedWinner_WhenVoidedWithAnyCode(string code)
    {
        var maria = Attendee("María");
        var juan = Attendee("Juan");
        var draws = new[] { VoidedDraw(PrizeP, maria, code) };

        var pool = EligiblePool.For([maria, juan], draws, onePrizePerCompany: false);

        pool.Members.ShouldBe([juan]);
        pool.ExcludedByVoid.ShouldBe(1);
    }

    [Fact]
    public void For_ReturnsAVoidedWinnersColleague_WhenOnePrizePerCompanyIsOn()
    {
        // Una anulación no es un premio: la empresa de María no se llevó nada.
        var maria = Attendee("María", company: "Hotel Sol");
        var juan = Attendee("Juan", company: "Hotel Sol");
        var draws = new[] { VoidedDraw(PrizeP, maria, PrizeDrawVoidCode.Declined) };

        var pool = EligiblePool.For([maria, juan], draws, onePrizePerCompany: true);

        pool.Members.ShouldBe([juan]);
        pool.ExcludedByCompany.ShouldBe(0);
    }

    [Fact]
    public void For_ReturnsAPersonCountedOnceByTheVoid_WhenTheyAlsoHoldALiveDrawElsewhere()
    {
        // La anulación en Q no toca el sorteo vivo de María en P: solo la saca de los bombos que quedan.
        var maria = Attendee("María");
        var juan = Attendee("Juan");
        var draws = new[] { LiveDraw(PrizeP, maria), VoidedDraw(PrizeQ, maria, PrizeDrawVoidCode.Absent) };

        var pool = EligiblePool.For([maria, juan], draws, onePrizePerCompany: false);

        pool.Members.ShouldBe([juan]);
        pool.ExcludedByVoid.ShouldBe(1);
        pool.ExcludedByPerson.ShouldBe(0);
    }

    [Fact]
    public void For_ReturnsAPoolWithoutALiveWinner_WhenOnePrizePerPersonIsOn()
    {
        var maria = Attendee("María");
        var juan = Attendee("Juan");
        var draws = new[] { LiveDraw(PrizeP, maria) };

        var pool = EligiblePool.For([maria, juan], draws, onePrizePerCompany: false);

        pool.Members.ShouldBe([juan]);
        pool.ExcludedByPerson.ShouldBe(1);
    }

    [Fact]
    public void For_ReturnsAPoolWithoutALiveWinner_Always()
    {
        var maria = Attendee("María");
        var juan = Attendee("Juan");
        var draws = new[] { LiveDraw(PrizeP, maria) };

        var pool = EligiblePool.For([maria, juan], draws, onePrizePerCompany: false);

        // Quien ganó no vuelve a entrar: no hay interruptor que lo cambie.
        pool.Members.ShouldBe(new[] { juan });
        pool.ExcludedByPerson.ShouldBe(1);
    }

    [Fact]
    public void For_ReturnsOnlyQualifiedAttendees_WhenNothingHasBeenDrawn()
    {
        var maria = Attendee("María");
        var walkIn = Attendee("Pedro", qualified: false);

        var pool = EligiblePool.For([maria, walkIn], [], onePrizePerCompany: false);

        pool.Members.ShouldBe([maria]);
        pool.Participants.ShouldBe(1);
    }

    #endregion

    #region Company matching

    [Fact]
    public void For_ReturnsAPoolWithoutTheSameCompanyIgnoringCaseAccentsAndSpaces_WhenOnePrizePerCompanyIsOn()
    {
        var winner = Attendee("María", company: "Turismo  Pérez");
        var colleague = Attendee("Juan", company: "turismo perez");

        var pool = EligiblePool.For([winner, colleague], [LiveDraw(PrizeP, winner)], onePrizePerCompany: true);

        pool.Members.ShouldBeEmpty();
        // La ganadora sale por su premio (uno por persona, siempre); su colega, por la empresa.
        pool.ExcludedByPerson.ShouldBe(1);
        pool.ExcludedByCompany.ShouldBe(1);
    }

    [Fact]
    public void For_ReturnsACompanyThatOnlyLooksAlike_WhenOnePrizePerCompanyIsOn()
    {
        var winner = Attendee("María", company: "MITUR");
        var other = Attendee("Juan", company: "Mitur SRL");

        var pool = EligiblePool.For([winner, other], [LiveDraw(PrizeP, winner)], onePrizePerCompany: true);

        pool.Members.ShouldBe([other]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void For_ReturnsEveryBlankCompany_WhenOnePrizePerCompanyIsOn(string? blank)
    {
        var winner = Attendee("María", company: blank);
        var others = new[] { Attendee("Juan", company: null), Attendee("Ana", company: ""), Attendee("Luis", company: "  ") };

        var pool = EligiblePool.For([winner, .. others], [LiveDraw(PrizeP, winner)], onePrizePerCompany: true);

        pool.Members.ShouldBe(others, ignoreOrder: true);
        pool.ExcludedByCompany.ShouldBe(0);
    }

    #endregion

    #region The counts

    [Fact]
    public void For_ReturnsEachPersonCountedOnTheFirstLineThatRemovesThem_WhenLinesOverlap()
    {
        // María tiene una anulación en P y un premio vivo en Q: cae en la primera línea, no en las dos.
        var maria = Attendee("María", company: "Hotel Sol");
        var draws = new[] { VoidedDraw(PrizeP, maria, PrizeDrawVoidCode.Absent), LiveDraw(PrizeQ, maria) };

        var pool = EligiblePool.For([maria], draws, onePrizePerCompany: true);

        pool.ExcludedByVoid.ShouldBe(1);
        pool.ExcludedByPerson.ShouldBe(0);
        pool.ExcludedByCompany.ShouldBe(0);
    }

    [Fact]
    public void For_ReturnsZeroForALine_WhenItsToggleIsOff()
    {
        var maria = Attendee("María", company: "Hotel Sol");
        var juan = Attendee("Juan", company: "Hotel Sol");

        var pool = EligiblePool.For([maria, juan], [LiveDraw(PrizeP, maria)], onePrizePerCompany: false);

        // Con la regla de empresa apagada, la colega entra; la ganadora sigue fuera por persona.
        pool.ExcludedByPerson.ShouldBe(1);
        pool.ExcludedByCompany.ShouldBe(0);
        pool.Count.ShouldBe(1);
    }

    [Fact]
    public void For_ReturnsCountsThatSumToParticipants_WhenThePoolIsEmpty()
    {
        var maria = Attendee("María", company: "Hotel Sol");
        var juan = Attendee("Juan", company: "Hotel  sol");
        var ana = Attendee("Ana");
        var draws = new[] { VoidedDraw(Guid.NewGuid(), ana, PrizeDrawVoidCode.Ineligible), LiveDraw(PrizeP, maria) };

        var pool = EligiblePool.For([maria, juan, ana], draws, onePrizePerCompany: true);

        pool.Count.ShouldBe(0);
        (pool.ExcludedByVoid + pool.ExcludedByPerson + pool.ExcludedByCompany).ShouldBe(pool.Participants);
        pool.Participants.ShouldBe(3);
    }

    #endregion

    [Fact]
    public void For_ReturnsMembersOrderedById_WhenTheyArriveInAnotherOrder()
    {
        var attendees = Enumerable.Range(0, 20).Select(i => Attendee($"P{i}")).ToList();
        var shuffled = attendees.OrderByDescending(a => a.IdentityKey).ToList();

        var pool = EligiblePool.For(shuffled, [], onePrizePerCompany: false);

        pool.Members.Select(a => a.Id).ShouldBe(attendees.Select(a => a.Id).OrderBy(id => id));
    }

    [Fact]
    public void For_IncludesReinstatedAttendee_WhenReinstatedAfterLatestVoid()
    {
        var maria = Attendee("María");
        var juan = Attendee("Juan");
        var voided = VoidedDraw(PrizeP, maria, PrizeDrawVoidCode.Absent);
        var mariaReinstated = maria with { ReinstatedAt = voided.VoidedAt!.Value.AddMinutes(1), ReinstatedById = Guid.NewGuid() };

        var pool = EligiblePool.For([mariaReinstated, juan], [voided], onePrizePerCompany: false);

        pool.Members.ShouldBe(new[] { mariaReinstated, juan }, ignoreOrder: true);
        pool.ExcludedByVoid.ShouldBe(0);
    }

    [Fact]
    public void For_ExcludesReinstatedAttendeeAgain_WhenVoidedAfterReinstatement()
    {
        var maria = Attendee("María");
        var juan = Attendee("Juan");
        var firstVoid = VoidedDraw(PrizeP, maria, PrizeDrawVoidCode.Absent);
        var mariaReinstated = maria with { ReinstatedAt = firstVoid.VoidedAt!.Value.AddMinutes(1), ReinstatedById = Guid.NewGuid() };
        var secondVoid = VoidedDraw(PrizeQ, maria, PrizeDrawVoidCode.Absent) with
        {
            VoidedAt = mariaReinstated.ReinstatedAt!.Value.AddMinutes(2),
        };

        var pool = EligiblePool.For([mariaReinstated, juan], [firstVoid, secondVoid], onePrizePerCompany: false);

        pool.Members.ShouldBe([juan]);
        pool.ExcludedByVoid.ShouldBe(1);
    }
}

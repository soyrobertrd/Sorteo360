using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// El contenido del acta a partir de lo leído bajo el bloqueo (spec 03 §4.2). Son hechos, no
/// redacción: el orden, las cuentas y lo que se deja fuera se deciden acá, y se prueban acá.
/// </summary>
public sealed class NotarialRecordContentTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 6, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = StartedAt.AddHours(2);
    private static readonly Guid Operator = Guid.NewGuid();

    [Fact]
    public void Build_ReturnsTheHeaderFacts_WhenGivenACompletedRaffle()
    {
        var raffle = Completed() with { OnePrizePerCompany = true };

        var content = Build(Facts(raffle, []), version: 1);

        content.RecordNumber.ShouldBe(NotarialRecordContent.NumberOf(raffle.Id));
        content.Version.ShouldBe(1);
        content.Event.Name.ShouldBe("Roadshow Bávaro");
        content.StartedAt.ShouldBe(StartedAt);
        content.CompletedAt.ShouldBe(CompletedAt);
        content.OnePrizePerPerson.ShouldBeTrue();
        content.OnePrizePerCompany.ShouldBeTrue();
        content.AwardsListApplied.ShouldBeFalse();
        content.Authorities.ShouldBe(raffle.Authorities);
    }

    [Fact]
    public void Build_ReturnsNoReplaces_WhenTheVersionIsTheFirst()
    {
        Build(Facts(Completed(), [])).Replaces.ShouldBeNull();
    }

    [Fact]
    public void Build_ReturnsTheVersionItReplaces_WhenReissued()
    {
        var replaces = new NotarialRecordReplaces(2, CompletedAt.AddDays(1));

        var content = Build(Facts(Completed(), []), version: 3, replaces);

        content.Version.ShouldBe(3);
        content.Replaces.ShouldBe(replaces);
    }

    [Fact]
    public void Build_Throws_WhenTheFirstVersionClaimsToReplaceAnother()
    {
        Should.Throw<ArgumentException>(() =>
            Build(Facts(Completed(), []), version: 1, new NotarialRecordReplaces(1, CompletedAt)));
    }

    [Fact]
    public void Build_Throws_WhenALaterVersionDoesNotSayWhatItReplaces()
    {
        Should.Throw<ArgumentException>(() => Build(Facts(Completed(), []), version: 2));
    }

    [Fact]
    public void NumberOf_ReturnsTheFirstEightHexDigitsInUpperCase_WhenCalled()
    {
        NotarialRecordContent.NumberOf(new Guid("3f9a1c02-0000-4000-8000-000000000000")).ShouldBe("3F9A1C02");
    }

    #region La aritmética

    [Fact]
    public void Build_ReturnsTheExcludedBrokenDownInTheSpecOrder_WhenSeveralReasonsApply()
    {
        var attendees = new[]
        {
            Excluded("Ana", ExclusionReason.DominicanDomain),
            Excluded("Beto", ExclusionReason.WalkIn),
            Excluded("Carla", ExclusionReason.DominicanDomain),
            Excluded("Dario", ExclusionReason.ManuallyExcluded),
            Excluded("Eva", ExclusionReason.Suspended),
            Qualified("Fede", "Pérez"),
        };

        var content = Build(Facts(Completed(), attendees));

        content.Arithmetic.ExcludedByReason.Select(r => (r.Reason, r.Count)).ShouldBe(
        [
            (ExclusionReason.WalkIn, 1),
            (ExclusionReason.Suspended, 1),
            (ExclusionReason.ManuallyExcluded, 1),
            (ExclusionReason.DominicanDomain, 2),
        ]);
    }

    [Fact]
    public void Build_ReturnsCheckedInAsQualifiedPlusNotQualified_WhenTheDoorIsMixed()
    {
        var attendees = new[]
        {
            Qualified("Ana", "Álvarez"), Qualified("Beto", "Báez"),
            Excluded("Carla", ExclusionReason.WalkIn), Excluded("Dario", ExclusionReason.DoNotInvite),
            Excluded("Eva", ExclusionReason.NotAwardsMember),
        };

        var arithmetic = Build(Facts(Completed(), attendees)).Arithmetic;

        arithmetic.CheckedIn.ShouldBe(5);
        arithmetic.Qualified.ShouldBe(2);
        arithmetic.NotQualified.ShouldBe(3);
        arithmetic.CheckedIn.ShouldBe(arithmetic.Qualified + arithmetic.NotQualified);
        arithmetic.ExcludedByReason.Sum(r => r.Count).ShouldBe(arithmetic.NotQualified);
    }

    #endregion

    [Fact]
    public void InEvaluationOrder_ReturnsEveryExclusionReason_WhenComparedWithTheConstants()
    {
        // Un motivo nuevo que no entre acá desaparece de la aritmética y la puerta deja de cuadrar.
        var constants = typeof(ExclusionReason).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);

        ExclusionReason.InEvaluationOrder.ShouldBe(constants, ignoreOrder: true);
    }

    #region Las decisiones manuales

    [Fact]
    public void Build_ReturnsEachManualDecisionByNameWithTheDecidersName_WhenSomeoneDecided()
    {
        var decidedAt = StartedAt.AddMinutes(-30);
        var included = Qualified("Ana", "Álvarez") with
        {
            Company = "Viajes Caribe",
            ManualDecision = AttendeeOverrideDecision.Included,
            ManualDecidedById = Operator,
            ManualDecidedAt = decidedAt,
            ManualNote = "Lo pidió el director",
            OverruledReason = ExclusionReason.DominicanDomain,
        };
        var names = new Dictionary<Guid, string?> { [Operator] = "Luis Gómez" };

        var content = NotarialRecordContent.Build(Facts(Completed(), [included, Qualified("Beto", "Báez")]), names, 1, null);

        var decision = content.ManualDecisions.ShouldHaveSingleItem();
        decision.Name.ShouldBe("Ana Álvarez");
        decision.Company.ShouldBe("Viajes Caribe");
        decision.Decision.ShouldBe(AttendeeOverrideDecision.Included);
        decision.OverruledReason.ShouldBe(ExclusionReason.DominicanDomain);
        decision.DecidedById.ShouldBe(Operator);
        decision.DecidedByName.ShouldBe("Luis Gómez");
        decision.DecidedAt.ShouldBe(decidedAt);
    }

    [Fact]
    public void Build_ReturnsTheInclusionWithoutEffect_WhenARuleNobodyOverridesBeatIt()
    {
        // Una inclusión sobre quien llegó sin invitación queda escrita, pero la persona no entra: el
        // acta tiene que decir que no surtió efecto, y no que entró (spec 01 §6).
        var walkIn = Excluded("Ana", ExclusionReason.WalkIn) with
        {
            ManualDecision = AttendeeOverrideDecision.Included,
            ManualDecidedById = Operator,
            ManualDecidedAt = StartedAt.AddMinutes(-5),
            OverruledReason = ExclusionReason.WalkIn,
        };
        var dominican = Qualified("Beto", "Báez") with
        {
            ManualDecision = AttendeeOverrideDecision.Included,
            ManualDecidedById = Operator,
            ManualDecidedAt = StartedAt.AddMinutes(-4),
            OverruledReason = ExclusionReason.DominicanDomain,
        };

        var decisions = Build(Facts(Completed(), [walkIn, dominican])).ManualDecisions;

        decisions[0].TookEffect.ShouldBeFalse();
        decisions[1].TookEffect.ShouldBeTrue();
    }

    [Fact]
    public void Build_ReturnsANullDeciderName_WhenTheRealmCouldNotResolveIt()
    {
        var excluded = Excluded("Ana", ExclusionReason.ManuallyExcluded) with
        {
            ManualDecision = AttendeeOverrideDecision.Excluded,
            ManualDecidedById = Operator,
            ManualDecidedAt = StartedAt.AddMinutes(-5),
        };

        var content = NotarialRecordContent.Build(
            Facts(Completed(), [excluded]), new Dictionary<Guid, string?> { [Operator] = null }, 1, null);

        content.ManualDecisions.ShouldHaveSingleItem().DecidedByName.ShouldBeNull();
    }

    [Fact]
    public void Build_ReturnsDecisionsThatCannotCarryTheOverrideNote_WhenTheirShapeIsInspected()
    {
        // La nota de la decisión manual es para el próximo operador, no para un documento notarial
        // (spec 03 §4.2 punto 8): no está en el contenido, así que ningún renderizador puede imprimirla.
        typeof(NotarialRecordDecision).GetProperties().Select(p => p.Name)
            .ShouldNotContain(name => name.Contains("Note", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Los resultados

    [Fact]
    public void Build_ReturnsRoundsByNumberWithPrizesInCreationOrderAndTheirLiveWinner_WhenSomeAreDrawn()
    {
        var winner = Qualified("Ana", "Álvarez") with { Company = "Viajes Caribe" };
        var roundTwo = Round(2, "Ronda final", Prize("Crucero", StartedAt.AddMinutes(-60)));
        var first = Prize("Bolsa", StartedAt.AddMinutes(-50));
        var second = Prize("Maleta", StartedAt.AddMinutes(-40), value: "USD 200");
        var roundOne = Round(1, "Primera ronda", second, first);
        var voided = Draw(first, winner, StartedAt.AddMinutes(5)) with
        {
            VoidCode = PrizeDrawVoidCode.Absent, VoidedAt = StartedAt.AddMinutes(6), VoidedById = Operator,
        };
        var live = Draw(first, winner, StartedAt.AddMinutes(7));

        var content = Build(Facts(Completed(), [winner], [roundTwo, roundOne], [live, voided]));

        content.Rounds.Select(r => r.Name).ShouldBe(["Primera ronda", "Ronda final"]);
        var prizes = content.Rounds[0].Prizes;
        prizes.Select(p => p.Name).ShouldBe(["Bolsa", "Maleta"]);
        prizes[0].WinnerName.ShouldBe("Ana Álvarez");
        prizes[0].WinnerCompany.ShouldBe("Viajes Caribe");
        prizes[1].WinnerName.ShouldBeNull();
        prizes[1].Value.ShouldBe("USD 200");
        content.PrizesAwarded.ShouldBe(1);
        content.PrizesNotAwarded.ShouldBe(2);
    }

    [Fact]
    public void Build_ReturnsAPrizeCreatedAfterTheStartFlagged_WhenItWasAddedDuringTheRaffle()
    {
        var round = Round(1, "Ronda", Prize("Antes", StartedAt.AddMinutes(-1)), Prize("Sorpresa", StartedAt.AddMinutes(30)));

        var prizes = Build(Facts(Completed(), [], [round])).Rounds[0].Prizes;

        prizes.Single(p => p.Name == "Antes").AddedDuringRaffle.ShouldBeFalse();
        prizes.Single(p => p.Name == "Sorpresa").AddedDuringRaffle.ShouldBeTrue();
    }

    [Fact]
    public void Build_ReturnsTheSponsorsCompanyNameOnly_WhenAPrizeIsSponsored()
    {
        var sponsorId = Guid.NewGuid();
        var sponsored = Prize("Ron", StartedAt.AddMinutes(-1)) with { SponsorCompanyId = sponsorId, SponsorContactId = Guid.NewGuid() };
        var round = Round(1, "Ronda", sponsored, Prize("Bolsa", StartedAt.AddMinutes(-1)));

        var prizes = NotarialRecordContent.Build(
            Facts(Completed(), [], [round]), new Dictionary<Guid, string?>(), 1, null,
            new Dictionary<Guid, string> { [sponsorId] = "Brugal & Co." }).Rounds[0].Prizes;

        prizes.Single(p => p.Name == "Ron").SponsorCompany.ShouldBe("Brugal & Co.");
        prizes.Single(p => p.Name == "Bolsa").SponsorCompany.ShouldBeNull();
        typeof(NotarialRecordPrize).GetProperties().Select(p => p.Name)
            .ShouldNotContain(name => name.Contains("Contact", StringComparison.Ordinal));
    }

    #endregion

    #region Las reglas

    [Fact]
    public void Build_ReturnsTheRaffleRulesUnchanged_WhenNothingWasDrawn()
    {
        var raffle = Completed() with { OnePrizePerPerson = false, OnePrizePerCompany = true };

        var content = Build(Facts(raffle, []));

        content.OnePrizePerPerson.ShouldBeFalse();
        content.OnePrizePerCompany.ShouldBeTrue();
        content.RulesChanged.ShouldBeFalse();
    }

    [Fact]
    public void Build_ReturnsTheDrawsRulesUnchanged_WhenTheyAgreeWithEachOtherButNotWithTheRaffle()
    {
        // Se cambiaron después del último sorteo: el acta dice las reglas bajo las que se sorteó.
        var raffle = Completed() with { OnePrizePerPerson = false, OnePrizePerCompany = true };
        var (round, draws) = TwoDraws((true, false), (true, false));

        var content = Build(Facts(raffle, [], [round], draws));

        content.OnePrizePerPerson.ShouldBeTrue();
        content.OnePrizePerCompany.ShouldBeFalse();
        content.RulesChanged.ShouldBeFalse();
        content.DrawLog.ShouldAllBe(l => l.OnePrizePerPerson && !l.OnePrizePerCompany);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Build_ReturnsRulesChangedWithEachDrawsOwn_WhenTheDrawsDisagree(bool person, bool company)
    {
        var (round, draws) = TwoDraws((true, false), (person, company));

        var content = Build(Facts(Completed(), [], [round], draws));

        content.RulesChanged.ShouldBeTrue();
        content.DrawLog.Select(l => (l.OnePrizePerPerson, l.OnePrizePerCompany)).ShouldBe([(true, false), (person, company)]);
    }

    /// <summary>Dos sorteos de una ronda, el primero anulado, cada uno con sus reglas.</summary>
    private static (RaffleRound Round, IReadOnlyList<PrizeDraw> Draws) TwoDraws(
        (bool Person, bool Company) first, (bool Person, bool Company) second)
    {
        var ana = Qualified("Ana", "Álvarez");
        var bolsa = Prize("Bolsa", StartedAt.AddMinutes(-10));
        var voided = Draw(bolsa, ana, StartedAt.AddMinutes(5)) with
        {
            OnePrizePerPerson = first.Person,
            OnePrizePerCompany = first.Company,
            VoidCode = PrizeDrawVoidCode.Declined,
            VoidedAt = StartedAt.AddMinutes(6),
            VoidedById = Operator,
        };
        var redraw = Draw(bolsa, ana, StartedAt.AddMinutes(8)) with
        {
            OnePrizePerPerson = second.Person,
            OnePrizePerCompany = second.Company,
        };
        return (Round(1, "Ronda", bolsa), [voided, redraw]);
    }

    #endregion

    #region El registro de sorteos

    [Fact]
    public void Build_ReturnsEveryDrawInTimeOrderVoidedOnesIncluded_WhenThePrizeWasRedrawn()
    {
        var ana = Qualified("Ana", "Álvarez");
        var beto = Qualified("Beto", "Báez");
        var bolsa = Prize("Bolsa", StartedAt.AddMinutes(-10));
        var round = Round(1, "Primera ronda", bolsa);
        var voided = Draw(bolsa, ana, StartedAt.AddMinutes(5), pool: 40) with
        {
            VoidCode = PrizeDrawVoidCode.Other,
            VoidNote = "Es empleado del hotel",
            VoidedAt = StartedAt.AddMinutes(6),
            VoidedById = Operator,
        };
        var redraw = Draw(bolsa, beto, StartedAt.AddMinutes(8), pool: 39);

        var log = Build(Facts(Completed(), [ana, beto], [round], [redraw, voided])).DrawLog;

        log.Select(l => l.WinnerName).ShouldBe(["Ana Álvarez", "Beto Báez"]);
        log[0].RoundNumber.ShouldBe(1);
        log[0].RoundName.ShouldBe("Primera ronda");
        log[0].PrizeName.ShouldBe("Bolsa");
        log[0].EligiblePoolSize.ShouldBe(40);
        log[0].VoidCode.ShouldBe(PrizeDrawVoidCode.Other);
        log[0].VoidNote.ShouldBe("Es empleado del hotel");
        log[0].VoidedAt.ShouldBe(StartedAt.AddMinutes(6));
        log[1].VoidCode.ShouldBeNull();
        log[1].EligiblePoolSize.ShouldBe(39);
    }

    #endregion

    #region El anexo

    [Fact]
    public void Build_ReturnsOnlyTheQualifiedSortedByLastThenFirstNameIgnoringAccentsAndCase_WhenBuildingTheAnnex()
    {
        var attendees = new[]
        {
            Qualified("Zoe", "Núñez"),
            Qualified("Ana", "alvarez2"),
            Qualified("Luis", "Ortiz"),
            Qualified("Bea", "Álvarez"),
            Qualified("Ana", "Álvarez"),
            Qualified("Carlos", "Nava"),
            Qualified("Pedro", "Zapata"),
            Excluded("Aaron", ExclusionReason.WalkIn) with { LastName = "Aaronson" },
        };

        var annex = Build(Facts(Completed(), attendees)).Annex;

        annex.Select(p => p.Name).ShouldBe(
        [
            "Ana Álvarez", "Bea Álvarez", "Ana alvarez2", "Carlos Nava", "Zoe Núñez", "Luis Ortiz", "Pedro Zapata",
        ]);
        annex.Select(p => p.Number).ShouldBe([1, 2, 3, 4, 5, 6, 7]);
    }

    [Fact]
    public void Build_ReturnsAnAnnexThatCannotCarryAnEmail_WhenItsShapeIsInspected()
    {
        typeof(NotarialRecordParticipant).GetProperties().Select(p => p.Name)
            .ShouldNotContain(name => name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    private static NotarialRecordContent Build(
        NotarialRecordFacts facts, int version = 1, NotarialRecordReplaces? replaces = null) =>
        NotarialRecordContent.Build(facts, new Dictionary<Guid, string?>(), version, replaces);

    private static Raffle Completed()
    {
        var started = Raffle.CreateDraft(Guid.NewGuid(), Operator, StartedAt.AddDays(-1)).Start(null, Operator, StartedAt).Value with
        {
            Authorities = new RaffleAuthorities("Ana Notaria", "001-1", "Luis Testigo", "002-2", "Eva Testigo", "003-3"),
            PickupInstructions = "Stand de MITUR",
        };
        return started.Complete(Operator, CompletedAt).Value;
    }

    private static NotarialRecordFacts Facts(
        Raffle raffle,
        IReadOnlyList<RaffleAttendee> attendees,
        IReadOnlyList<RaffleRound>? rounds = null,
        IReadOnlyList<PrizeDraw>? draws = null) => new(
        new RaffleWithRounds(raffle, rounds ?? []) { Draws = draws ?? [] },
        attendees,
        new NotarialRecordEvent(
            "Roadshow Bávaro", new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 7),
            [new NotarialRecordPlace("Punta Cana", "República Dominicana", CityTimeZone.Create("America/Santo_Domingo").Value)]));

    private static RaffleAttendee Qualified(string first, string last) => new(
        Guid.NewGuid(), $"{Guid.NewGuid():N}@agencia.com", Guid.NewGuid(), first, last, null, StartedAt.AddHours(-1),
        Qualified: true, ExclusionReason: null, null, null, null, null, null);

    private static RaffleAttendee Excluded(string first, string reason) =>
        Qualified(first, "Excluido") with { Qualified = false, ExclusionReason = reason };

    private static Prize Prize(string name, DateTimeOffset createdAt, string? value = null) =>
        new(Guid.NewGuid(), Guid.Empty, name, null, value, createdAt, createdAt, Operator, Operator);

    private static RaffleRound Round(int number, string name, params Prize[] prizes)
    {
        var id = Guid.NewGuid();
        return new RaffleRound(
            id, Guid.Empty, number, name, null, prizes.Select(p => p with { RoundId = id }).ToList(),
            StartedAt.AddDays(-1), StartedAt.AddDays(-1), Operator, Operator);
    }

    private static PrizeDraw Draw(Prize prize, RaffleAttendee winner, DateTimeOffset at, int pool = 10) => new(
        Guid.NewGuid(), prize.Id, winner.Id, winner.DisplayName, winner.Company, winner.EmailNormalized,
        at, Operator, pool, true, false, "RD-7K3MQ9XA", null, null, null, null);

    [Fact]
    public void Build_IncludesActiveReinstatements_WhenVoidedAttendeesWereReinstated()
    {
        var prize = Prize("Televisor", StartedAt.AddMinutes(-5));
        var round = Round(1, "Ronda 1", prize);
        var ana = Qualified("Ana", "Álvarez") with
        {
            ReinstatedAt = StartedAt.AddMinutes(15),
            ReinstatedById = Operator,
        };
        var voidedDraw = Draw(prize, ana, StartedAt.AddMinutes(10)) with
        {
            VoidCode = PrizeDrawVoidCode.Absent,
            VoidedAt = StartedAt.AddMinutes(11),
            VoidedById = Operator,
        };
        var operatorNames = new Dictionary<Guid, string?> { [Operator] = "Carlos Operador" };

        var content = NotarialRecordContent.Build(
            Facts(Completed(), [ana], [round], [voidedDraw]),
            operatorNames,
            1,
            null);

        var reinstatement = content.Reinstatements.ShouldHaveSingleItem();
        reinstatement.Name.ShouldBe("Ana Álvarez");
        reinstatement.ReinstatedByName.ShouldBe("Carlos Operador");
    }
}

using NSubstitute;
using Shouldly;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class PrizeDrawSummaryOperatorNameTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 18, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_PopulatesDrawnVoidedAndRedeemedOperatorNames_WhenDictionaryProvided()
    {
        var drawnById = Guid.NewGuid();
        var voidedById = Guid.NewGuid();
        var redeemedById = Guid.NewGuid();
        var drawId = Guid.NewGuid();

        var handover = new PrizeHandover(
            Id: Guid.NewGuid(),
            PrizeDrawId: drawId,
            ReceiverName: "Ana Pérez",
            ReceiverIdentityDocument: "001-0000000-1",
            ReceiverIsWinner: true,
            SignatureStoredFileId: Guid.NewGuid(),
            RedeemedAt: Now.AddMinutes(5),
            RedeemedById: redeemedById,
            ReversedAt: null,
            ReversedById: null,
            ReversalReason: null);

        var draw = new PrizeDraw(
            Id: drawId,
            PrizeId: Guid.NewGuid(),
            WinnerAttendeeId: Guid.NewGuid(),
            WinnerName: "Ana Pérez",
            WinnerCompany: "Empresa Demo",
            WinnerEmail: "ana@empresa.local",
            DrawnAt: Now,
            DrawnById: drawnById,
            EligiblePoolSize: 25,
            OnePrizePerPerson: true,
            OnePrizePerCompany: false,
            RedemptionCode: "RD-ABCD-1234",
            VoidCode: PrizeDrawVoidCode.Absent,
            VoidNote: "No se presentó",
            VoidedAt: Now.AddMinutes(1),
            VoidedById: voidedById)
        {
            Handovers = [handover],
        };

        var names = new Dictionary<Guid, string?>
        {
            [drawnById] = "Operador Uno",
            [voidedById] = "Supervisor Dos",
            [redeemedById] = "Mesa Entrega",
        };

        var summary = PrizeDrawSummary.From(draw, names);

        summary.DrawnByName.ShouldBe("Operador Uno");
        summary.Void.ShouldNotBeNull();
        summary.Void!.VoidedByName.ShouldBe("Supervisor Dos");
        summary.Handovers.Single().RedeemedByName.ShouldBe("Mesa Entrega");
    }

    [Fact]
    public async Task ResolveOperatorNamesAsync_SkipsDirectory_WhenDrawsAreEmpty()
    {
        var operators = Substitute.For<IRaffleOperatorDirectory>();

        var result = await RaffleResponse.ResolveOperatorNamesAsync([], operators, CancellationToken.None);

        result.ShouldBeNull();
        await operators.DidNotReceiveWithAnyArgs().ResolveNamesAsync(default!, default);
    }

    [Fact]
    public async Task ResolveOperatorNamesAsync_QueriesDistinctOperatorIdsAcrossDrawsVoidsAndHandovers()
    {
        var drawnById = Guid.NewGuid();
        var voidedById = Guid.NewGuid();
        var redeemedById = Guid.NewGuid();
        var drawId = Guid.NewGuid();

        var draw = new PrizeDraw(
            Id: drawId,
            PrizeId: Guid.NewGuid(),
            WinnerAttendeeId: Guid.NewGuid(),
            WinnerName: "Carlos",
            WinnerCompany: null,
            WinnerEmail: "carlos@empresa.local",
            DrawnAt: Now,
            DrawnById: drawnById,
            EligiblePoolSize: 10,
            OnePrizePerPerson: true,
            OnePrizePerCompany: false,
            RedemptionCode: "RD-1111-2222",
            VoidCode: PrizeDrawVoidCode.Absent,
            VoidNote: null,
            VoidedAt: Now.AddMinutes(2),
            VoidedById: voidedById)
        {
            Handovers =
            [
                new PrizeHandover(
                    Guid.NewGuid(), drawId,
                    "Carlos", "001-0000000-2", true,
                    Guid.NewGuid(), Now.AddMinutes(3), redeemedById,
                    null, null, null),
            ],
        };

        IReadOnlyCollection<Guid>? capturedIds = null;
        var operators = Substitute.For<IRaffleOperatorDirectory>();
        operators.ResolveNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedIds = call.Arg<IReadOnlyCollection<Guid>>();
                return new Dictionary<Guid, string?>
                {
                    [drawnById] = "Operador A",
                    [voidedById] = "Operador B",
                    [redeemedById] = null,
                };
            });

        var resolved = await RaffleResponse.ResolveOperatorNamesAsync([draw], operators, CancellationToken.None);

        capturedIds.ShouldNotBeNull();
        capturedIds!.ShouldBe([drawnById, voidedById, redeemedById], ignoreOrder: true);
        resolved![drawnById].ShouldBe("Operador A");
        resolved[voidedById].ShouldBe("Operador B");
        resolved[redeemedById].ShouldBeNull();
    }
}

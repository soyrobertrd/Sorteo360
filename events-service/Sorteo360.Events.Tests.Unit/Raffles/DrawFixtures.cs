using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>Asistentes congelados y sorteos de premio armados a mano para los tests del bombo.</summary>
internal static class DrawFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 10, 4, 21, 0, 0, TimeSpan.Zero);

    public static RaffleAttendee Attendee(string first, string? company = null, bool qualified = true, Guid? id = null) => new(
        Id: id ?? Guid.NewGuid(),
        EmailNormalized: $"{first.ToLowerInvariant()}@agencia.com",
        ContactId: Guid.NewGuid(),
        FirstName: first,
        LastName: "Pérez",
        Company: company,
        CheckedInAt: Now.AddHours(-2),
        Qualified: qualified,
        ExclusionReason: qualified ? null : ExclusionReason.WalkIn,
        ManualDecision: null,
        ManualDecidedById: null,
        ManualDecidedAt: null,
        ManualNote: null,
        OverruledReason: null);

    public static PrizeDraw LiveDraw(Guid prizeId, RaffleAttendee winner) => new(
        Id: Guid.NewGuid(),
        PrizeId: prizeId,
        WinnerAttendeeId: winner.Id,
        WinnerName: $"{winner.FirstName} {winner.LastName}",
        WinnerCompany: winner.Company,
        WinnerEmail: winner.EmailNormalized,
        DrawnAt: Now,
        DrawnById: Guid.NewGuid(),
        EligiblePoolSize: 1,
        OnePrizePerPerson: true,
        OnePrizePerCompany: false,
        RedemptionCode: RedemptionCode.Generate(),
        VoidCode: null,
        VoidNote: null,
        VoidedAt: null,
        VoidedById: null);

    public static PrizeDraw VoidedDraw(Guid prizeId, RaffleAttendee winner, string code) => LiveDraw(prizeId, winner) with
    {
        VoidCode = code,
        VoidedAt = Now.AddMinutes(1),
        VoidedById = Guid.NewGuid(),
    };

    /// <summary>Una entrega viva del sorteo, firmada y con la persona que retiró.</summary>
    public static PrizeHandover LiveHandover(PrizeDraw draw, DateTimeOffset? at = null) => new(
        Id: Guid.NewGuid(),
        PrizeDrawId: draw.Id,
        ReceiverName: draw.WinnerName,
        ReceiverIdentityDocument: "001-1234567-8",
        ReceiverIsWinner: true,
        SignatureStoredFileId: Guid.NewGuid(),
        RedeemedAt: at ?? Now.AddDays(1),
        RedeemedById: Guid.NewGuid(),
        ReversedAt: null,
        ReversedById: null,
        ReversalReason: null);

    /// <summary>Una entrega revertida: la fila queda, con el motivo.</summary>
    public static PrizeHandover ReversedHandover(PrizeDraw draw, DateTimeOffset? at = null) => LiveHandover(draw, at) with
    {
        ReversedAt = (at ?? Now.AddDays(1)).AddMinutes(10),
        ReversedById = Guid.NewGuid(),
        ReversalReason = "Se marcó el premio equivocado.",
    };

    /// <summary>El sorteo con esas entregas, de la más nueva a la más vieja como las lee el repositorio.</summary>
    public static PrizeDraw With(this PrizeDraw draw, params PrizeHandover[] handovers) => draw with
    {
        Handovers = handovers.OrderByDescending(h => h.RedeemedAt).ToList(),
    };
}

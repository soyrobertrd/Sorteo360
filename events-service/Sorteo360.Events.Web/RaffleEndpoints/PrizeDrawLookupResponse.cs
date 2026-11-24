using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Lo que el mostrador ve de un código de retiro (spec 03 §2.4): de qué premio es, quién lo ganó y en
/// qué está. Un sorteo anulado se responde con su anulación, y uno entregado con su entrega viva.
/// </summary>
public record PrizeDrawLookupResponse
{
    public Guid DrawId { get; init; }
    public Guid EventId { get; init; }
    public Guid RaffleId { get; init; }
    public Guid PrizeId { get; init; }
    public string PrizeName { get; init; } = string.Empty;
    public string WinnerName { get; init; } = string.Empty;
    public string? WinnerCompany { get; init; }

    /// <summary><c>drawn</c>, <c>redeemed</c> o <c>voided</c>.</summary>
    public string Status { get; init; } = PrizeDrawStatus.Drawn;

    /// <summary>La anulación, solo si el sorteo está anulado.</summary>
    public PrizeDrawLookupVoid? Void { get; init; }

    /// <summary>La entrega viva, solo si el premio fue entregado; una revertida no aparece.</summary>
    public PrizeDrawLookupHandover? Handover { get; init; }

    public static PrizeDrawLookupResponse From(PrizeDrawLookup lookup)
    {
        var draw = lookup.Draw;
        var handover = draw.IsLive ? draw.LiveHandover : null;

        return new PrizeDrawLookupResponse
        {
            DrawId = draw.Id,
            EventId = lookup.EventId,
            RaffleId = lookup.RaffleId,
            PrizeId = draw.PrizeId,
            PrizeName = lookup.PrizeName,
            WinnerName = draw.WinnerName,
            WinnerCompany = draw.WinnerCompany,
            Status = PrizeDrawStatus.Of(draw),
            Void = draw.IsLive ? null : new PrizeDrawLookupVoid(draw.VoidCode!, draw.VoidedAt!.Value),
            Handover = handover is null ? null : new PrizeDrawLookupHandover(handover.ReceiverName, handover.RedeemedAt),
        };
    }
}

/// <summary>Por qué y cuándo se anuló el sorteo del código.</summary>
public sealed record PrizeDrawLookupVoid(string Code, DateTimeOffset VoidedAt);

/// <summary>Quién retiró el premio y cuándo.</summary>
public sealed record PrizeDrawLookupHandover(string ReceiverName, DateTimeOffset RedeemedAt);

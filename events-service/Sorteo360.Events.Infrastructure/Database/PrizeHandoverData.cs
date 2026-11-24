namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.prize_handover</c> (una entrega de un premio, viva
/// o revertida, con quién retiró y su firma).
/// </summary>
internal sealed class PrizeHandoverData
{
    public Guid Id { get; set; }
    public Guid PrizeDrawId { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverIdentityDocument { get; set; } = string.Empty;
    public bool ReceiverIsWinner { get; set; }
    public Guid SignatureStoredFileId { get; set; }
    public DateTimeOffset RedeemedAt { get; set; }
    public Guid RedeemedById { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }
    public string? ReversalReason { get; set; }
}

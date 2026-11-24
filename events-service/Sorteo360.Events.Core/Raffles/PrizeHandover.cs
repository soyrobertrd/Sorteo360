namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// La entrega de un premio: que llegó a manos de una persona con nombre y documento, que firmó
/// (spec 03 §3). Cuelga del sorteo y no del premio, porque lo que se retira es lo que ganó ese
/// sorteo.
/// <para>
/// Una reversión no borra la fila: le pone cuándo, quién y por qué (spec 03 §3.3), y la firma sigue
/// referenciada. Viva es la que no tiene reversión, y un sorteo tiene a lo sumo una viva.
/// </para>
/// </summary>
public sealed record PrizeHandover(
    Guid Id,
    Guid PrizeDrawId,
    string ReceiverName,
    string ReceiverIdentityDocument,
    bool ReceiverIsWinner,
    Guid SignatureStoredFileId,
    DateTimeOffset RedeemedAt,
    Guid RedeemedById,
    DateTimeOffset? ReversedAt,
    Guid? ReversedById,
    string? ReversalReason)
{
    /// <summary>Si sigue en pie: nadie la revirtió.</summary>
    public bool IsLive => ReversedAt is null;
}

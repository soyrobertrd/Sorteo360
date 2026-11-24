namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Un sorteo de premio tal como sale del servicio, en la respuesta del premio y dentro de
/// <c>PRIZE_ALREADY_DRAWN</c>: una sola forma para los dos, así que un reintento recupera al ganador
/// con los mismos campos. Sin correo. Lleva el código de retiro, su estado y sus entregas, que es lo
/// que la lectura del sorteo y la entrega muestran del premio (spec 03 §3.4), y si al ganador se le
/// avisó: el estado del correo y el lote que lo llevó, que se consulta en
/// <c>GET /v1/notifications/batches/{id}</c> (ADR-0023). Los dos son nulos hasta completar. Lleva
/// también las dos reglas bajo las que se sorteó, que pueden no ser las que el sorteo tiene ahora
/// (spec 02 §1.2).
/// </summary>
public sealed record PrizeDrawSummary(
    Guid Id,
    Guid WinnerAttendeeId,
    string WinnerName,
    string? WinnerCompany,
    DateTimeOffset DrawnAt,
    int EligiblePoolSize,
    bool OnePrizePerPerson,
    bool OnePrizePerCompany,
    string RedemptionCode,
    string Status,
    PrizeDrawVoidSummary? Void,
    IReadOnlyList<PrizeHandoverSummary> Handovers,
    string? WinnerNotificationStatus,
    string? WinnerNotificationId,
    string? DrawnByName = null,
    string? WinnerTicketNumber = null,
    string? WinnerEmployeeCode = null,
    string? WinnerDepartment = null,
    string? WinnerLocation = null,
    string? WinnerFirstName = null,
    string? WinnerLastName = null)
{
    public static PrizeDrawSummary From(PrizeDraw draw) => From(draw, operatorNames: null);

    public static PrizeDrawSummary From(PrizeDraw draw, IReadOnlyDictionary<Guid, string?>? operatorNames)
    {
        ArgumentNullException.ThrowIfNull(draw);

        var drawnByName = ResolveName(operatorNames, draw.DrawnById);
        var voidedByName = draw.VoidedById.HasValue ? ResolveName(operatorNames, draw.VoidedById.Value) : null;

        return new PrizeDrawSummary(
            draw.Id,
            draw.WinnerAttendeeId,
            draw.WinnerName,
            draw.WinnerCompany,
            draw.DrawnAt,
            draw.EligiblePoolSize,
            draw.OnePrizePerPerson,
            draw.OnePrizePerCompany,
            draw.RedemptionCode,
            PrizeDrawStatus.Of(draw),
            draw.IsLive ? null : new PrizeDrawVoidSummary(draw.VoidCode!, draw.VoidNote, draw.VoidedAt!.Value, voidedByName),
            draw.Handovers.Select(h => PrizeHandoverSummary.From(h, operatorNames)).ToList(),
            draw.WinnerNotificationStatus,
            draw.WinnerNotificationId,
            drawnByName,
            draw.WinnerTicketNumber,
            draw.WinnerEmployeeCode,
            draw.WinnerDepartment,
            draw.WinnerLocation,
            draw.WinnerFirstName,
            draw.WinnerLastName);
    }

    internal static string? ResolveName(IReadOnlyDictionary<Guid, string?>? operatorNames, Guid userId) =>
        operatorNames is not null && operatorNames.TryGetValue(userId, out var name) ? name : null;
}

/// <summary>La anulación de un sorteo: el motivo, la nota, cuándo y quién la ejecutó.</summary>
public sealed record PrizeDrawVoidSummary(
    string Code,
    string? Note,
    DateTimeOffset VoidedAt,
    string? VoidedByName = null);

/// <summary>
/// Una entrega del sorteo, viva o revertida: quién retiró, con qué documento, si era el ganador según
/// el operador, su firma y, si se revirtió, cuándo, quién y por qué. Viva es la que no tiene
/// <c>ReversedAt</c>.
/// </summary>
public sealed record PrizeHandoverSummary(
    Guid Id,
    string ReceiverName,
    string ReceiverIdentityDocument,
    bool ReceiverIsWinner,
    Guid SignatureStoredFileId,
    DateTimeOffset RedeemedAt,
    Guid RedeemedById,
    DateTimeOffset? ReversedAt,
    Guid? ReversedById,
    string? ReversalReason,
    string? RedeemedByName = null)
{
    public static PrizeHandoverSummary From(PrizeHandover handover) => From(handover, operatorNames: null);

    public static PrizeHandoverSummary From(PrizeHandover handover, IReadOnlyDictionary<Guid, string?>? operatorNames)
    {
        ArgumentNullException.ThrowIfNull(handover);

        return new PrizeHandoverSummary(
            handover.Id,
            handover.ReceiverName,
            handover.ReceiverIdentityDocument,
            handover.ReceiverIsWinner,
            handover.SignatureStoredFileId,
            handover.RedeemedAt,
            handover.RedeemedById,
            handover.ReversedAt,
            handover.ReversedById,
            handover.ReversalReason,
            PrizeDrawSummary.ResolveName(operatorNames, handover.RedeemedById));
    }
}

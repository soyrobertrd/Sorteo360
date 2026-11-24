namespace Sorteo360.Notifications.Core.DTOs.History;

/// <summary>
/// Una fila del historial: un correo que se le mandó (o se intentó mandar) a una persona.
/// <para>
/// Lleva los campos del envío que la enmarcan —tipo, asunto, remitente— porque una fila suelta sin
/// ellos no se puede leer en una lista. El cuerpo no: es idéntico para todas las filas del mismo
/// envío y se lee entero en <c>batches/{batchId}</c>.
/// </para>
/// <para>
/// <c>DeliveryStatus</c> es el estado <b>reportado</b>, ya con el tope de
/// <see cref="History.DeliveryTracking"/> aplicado; <c>DeliveryTrackingAvailable</c> es lo que
/// le dice a la pantalla si ese estado puede moverse todavía.
/// </para>
/// </summary>
public sealed record HistoryRecipientDto(
    Guid Id,
    Guid BatchId,
    string Type,
    string Subject,
    string Language,
    string FromAddress,
    string? FromName,
    string Email,
    string? Name,
    string DeliveryStatus,
    string RecipientAction,
    bool DeliveryTrackingAvailable,
    DateTimeOffset QueuedAt,
    DateTimeOffset? SentAt,
    int AttemptCount,
    string? LastError,
    string? SgMessageId,
    string? SmtpQueuedAs,
    IReadOnlyList<HistoryAboutDto> About);

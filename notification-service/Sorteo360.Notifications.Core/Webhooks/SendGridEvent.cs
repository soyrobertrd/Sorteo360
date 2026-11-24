namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// Una entrada del lote del webhook, ya parseada, con su JSON original al lado.
/// </summary>
/// <param name="EventType">
/// El tipo tal como lo escribió el proveedor. No se valida contra
/// <see cref="SendGridEventTypes"/>: uno desconocido se guarda igual y solo se ignora al derivar
/// el estado.
/// </param>
/// <param name="Email">La dirección del evento, tal como vino.</param>
/// <param name="Timestamp">El instante del evento, en segundos Unix.</param>
/// <param name="SgEventId">El identificador del proveedor, si lo trajo.</param>
/// <param name="SgMessageId">
/// El identificador del mensaje, si lo trajo. Falta en los rebotes diferidos y asíncronos, que es
/// uno de los dos agujeros que obligan a la escalera de <see cref="EventCorrelation"/>.
/// </param>
/// <param name="CorrelationId">
/// El GUID opaco de la fila de persona, que viajó en <c>unique_args</c>. Es el único dato nuestro
/// que sale hacia el proveedor y el único que vuelve.
/// </param>
/// <param name="Payload">
/// El JSON de <b>esta</b> entrada, tal como llegó. Se guarda entero: lo que hoy no se lee es lo
/// que mañana explica un evento raro sin tener que pedírselo otra vez al proveedor.
/// </param>
public sealed record SendGridEvent(
    string EventType,
    string Email,
    long Timestamp,
    string? SgEventId,
    string? SgMessageId,
    Guid? CorrelationId,
    string Payload)
{
    /// <summary>El instante del evento como fecha.</summary>
    public DateTimeOffset OccurredAt => DateTimeOffset.FromUnixTimeSeconds(Timestamp);

    /// <summary>
    /// La clave de reserva contra la que deduplicar, o <c>null</c> cuando el evento trae
    /// <c>sg_event_id</c> y el índice único de esa columna ya hace el trabajo. Cada columna dice
    /// una sola cosa: <c>sg_event_id</c> es el identificador del proveedor, y esta es la que se usa
    /// <b>en su lugar</b> cuando no vino.
    /// </summary>
    public string? DedupeKey => string.IsNullOrWhiteSpace(SgEventId)
        ? EventDedupeKey.Composite(EventType, Email, Timestamp, SgMessageId)
        : null;
}

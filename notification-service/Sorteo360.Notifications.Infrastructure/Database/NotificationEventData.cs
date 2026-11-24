namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia de la tabla <c>notifications.notification_event</c>: un evento del
/// webhook del proveedor, con su JSON crudo.
/// <para>
/// <c>RecipientId</c> es nullable y ahí viven los huérfanos: un evento que llegó sin poder
/// correlacionarse con ninguna fila es un evento con la columna vacía, no una tabla aparte. Así
/// comparten consulta, y rellenar la columna después es una actualización y no una migración.
/// </para>
/// </summary>
internal sealed class NotificationEventData
{
    public Guid Id { get; set; }
    public string? SgEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string Payload { get; set; } = string.Empty;
    public Guid? RecipientId { get; set; }

    /// <summary>
    /// La clave de reserva contra la que deduplicar, solo para los eventos que llegan <b>sin</b>
    /// <c>sg_event_id</c>. Null cuando hay identificador del proveedor: ahí el índice único de esa
    /// columna ya hace el trabajo, y guardar el hash además solo daría dos formas de decir lo
    /// mismo.
    /// </summary>
    public string? DedupeKey { get; set; }
}

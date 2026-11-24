namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia de la tabla <c>notifications.notification_recipient</c>: una persona del
/// envío. Su id es el único dato de la fila que viaja al proveedor, y es opaco a propósito.
/// </summary>
internal sealed class NotificationRecipientData
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public string RecipientAction { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? LastError { get; set; }
    public string? SgMessageId { get; set; }
    public string? SmtpQueuedAs { get; set; }
}

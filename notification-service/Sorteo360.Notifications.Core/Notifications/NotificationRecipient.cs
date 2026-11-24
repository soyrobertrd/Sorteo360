namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Una persona del envío, y todo lo que se sabe de su correo. Una fila es siempre exactamente una
/// persona: no hay <c>cc</c> ni <c>bcc</c> en ningún lado.
/// <para>
/// El id es opaco y es lo único de esta fila que viaja al proveedor, para poder correlacionar sus
/// eventos con la fila sin publicar ningún id de negocio.
/// </para>
/// </summary>
public sealed record NotificationRecipient
{
    /// <summary>Largo máximo del nombre, el mismo en el contrato y en la columna.</summary>
    public const int NameMaxLength = 200;

    public Guid Id { get; init; }
    public Guid BatchId { get; init; }
    public string Email { get; init; }

    /// <summary>
    /// Nombre al que se dirigió el correo, tal como lo mandó quien llama. Es un hecho del envío,
    /// no un dato que haya que mantener al día: si la persona cambia de nombre mañana, este correo
    /// igual se le mandó con el de hoy.
    /// </summary>
    public string? Name { get; init; }
    public string DeliveryStatus { get; init; }
    public string RecipientAction { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset? NextAttemptAt { get; init; }
    public DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public string? LastError { get; init; }
    public string? SgMessageId { get; init; }
    public string? SmtpQueuedAs { get; init; }

    public NotificationRecipient(
        Guid id,
        Guid batchId,
        string email,
        string? name,
        string deliveryStatus,
        string recipientAction,
        int attemptCount,
        DateTimeOffset? nextAttemptAt,
        DateTimeOffset queuedAt,
        DateTimeOffset? sentAt,
        string? lastError,
        string? sgMessageId,
        string? smtpQueuedAs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientAction);

        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));
        }

        Id = id;
        BatchId = batchId;
        Email = email;
        Name = name;
        DeliveryStatus = deliveryStatus;
        RecipientAction = recipientAction;
        AttemptCount = attemptCount;
        NextAttemptAt = nextAttemptAt;
        QueuedAt = queuedAt;
        SentAt = sentAt;
        LastError = lastError;
        SgMessageId = sgMessageId;
        SmtpQueuedAs = smtpQueuedAs;
    }

    /// <summary>
    /// Fila recién aceptada: en cola, sin acción de la persona, sin intentos y sin nada que el
    /// proveedor haya dicho todavía.
    /// </summary>
    public static NotificationRecipient Queued(Guid batchId, string email, string? name, DateTimeOffset queuedAt) =>
        new(
            id: Guid.NewGuid(),
            batchId: batchId,
            email: email,
            name: name,
            deliveryStatus: Notifications.DeliveryStatus.Queued,
            recipientAction: Notifications.RecipientAction.None,
            attemptCount: 0,
            nextAttemptAt: null,
            queuedAt: queuedAt,
            sentAt: null,
            lastError: null,
            sgMessageId: null,
            smtpQueuedAs: null);
}

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Lo que el transporte contesta por un lote aceptado: una entrada por destinatario (spec
/// <c>04</c> §2.1).
/// </summary>
public sealed record SendBatchResult(IReadOnlyList<RecipientHandoffOutcome> Outcomes);

/// <summary>
/// Qué pasó con una persona del lote. Solo puede acabar en uno de los dos estados terminales de la
/// entrega: <c>handed_off</c> o <c>failed</c>.
/// <para>
/// <c>suppressed</c> no cabe acá a propósito: esa decisión la toma el CRM dentro de la petición de
/// lanzamiento (spec <c>04</c> §3), y esas filas nunca llegan al transporte.
/// </para>
/// </summary>
public sealed record RecipientHandoffOutcome
{
    public Guid RecipientId { get; init; }

    /// <summary>Ver <see cref="RecipientSendStatus"/>.</summary>
    public string SendStatus { get; init; }

    /// <summary>El motivo que mandó el transporte. Nulo cuando la entrega fue aceptada.</summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// El id del mensaje en notification-service, con el que después se lee si llegó (PGTI-177).
    /// Nulo en un rechazo, y con el transporte de stub, que no tiene a quién preguntarle.
    /// </summary>
    public Guid? NotificationId { get; init; }

    public RecipientHandoffOutcome(Guid recipientId, string sendStatus, string? failureReason, Guid? notificationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sendStatus);

        if (sendStatus is not (RecipientSendStatus.HandedOff or RecipientSendStatus.Failed))
            throw new ArgumentException($"Desenlace de entrega no admitido: '{sendStatus}'.", nameof(sendStatus));

        RecipientId = recipientId;
        SendStatus = sendStatus;
        FailureReason = failureReason;
        NotificationId = notificationId;
    }

    /// <summary>El transporte se hizo cargo. Está a dos pasos de "entregado", nunca es lo mismo.</summary>
    public static RecipientHandoffOutcome Accepted(Guid recipientId, Guid? notificationId = null) =>
        new(recipientId, RecipientSendStatus.HandedOff, failureReason: null, notificationId);

    /// <summary>
    /// El transporte miró a esta persona y la rechazó. Es terminal: en la práctica solo atrapa una
    /// dirección mal formada.
    /// </summary>
    public static RecipientHandoffOutcome Rejected(Guid recipientId, string reason) =>
        new(recipientId, RecipientSendStatus.Failed, reason);
}

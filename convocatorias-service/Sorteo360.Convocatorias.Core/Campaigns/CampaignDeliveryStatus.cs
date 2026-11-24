namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Qué pasó con el correo de un destinatario después de entregarlo al proveedor (PGTI-177). Es un
/// eje aparte de <see cref="RecipientSendStatus"/>, que es nuestro: este lo dicta el webhook del
/// proveedor a notification-service, y acá solo se lee.
/// <para>
/// <b>Abierto y pulsado quedan fuera a propósito</b>: nadie pidió seguimiento de aperturas, y el
/// proveedor los tiene apagados. Sin eventos (Mailpit, o un entorno sin webhook) todo se queda en
/// <see cref="Sent"/>, que es lo único que se sabe.
/// </para>
/// </summary>
public static class CampaignDeliveryStatus
{
    /// <summary>Salió y no se sabe más. También es lo que se ve con notification-service caído.</summary>
    public const string Sent = "sent";

    /// <summary>El proveedor lo dejó en el buzón.</summary>
    public const string Delivered = "delivered";

    /// <summary>El buzón lo rechazó.</summary>
    public const string Bounced = "bounced";

    /// <summary>La persona lo reportó como spam.</summary>
    public const string SpamReport = "spam_report";

    /// <summary>La persona se dio de baja desde el proveedor.</summary>
    public const string Unsubscribed = "unsubscribed";

    /// <summary>
    /// De varios, el que más dice: lo que hizo la persona antes que hasta dónde llegó el correo,
    /// porque un reporte de spam prueba que llegó.
    /// </summary>
    private static readonly string[] Precedence = [Unsubscribed, SpamReport, Bounced, Delivered, Sent];

    /// <summary>
    /// Traduce los dos ejes de notification-service, la entrega y la reacción, a este. Lo que no es
    /// una de las cuatro cosas que se muestran (en cola, diferido, descartado, fallido) se lee como
    /// <see cref="Sent"/>.
    /// </summary>
    public static string FromNotification(string? deliveryStatus, string? recipientAction) =>
        recipientAction switch
        {
            "unsubscribed" => Unsubscribed,
            "complained" => SpamReport,
            _ => deliveryStatus switch
            {
                "bounced" => Bounced,
                "delivered" => Delivered,
                _ => Sent,
            },
        };

    /// <summary>
    /// Lo que muestra una fila: nulo si nunca se entregó al proveedor, y si se entregó, lo que
    /// notification-service sabe de sus mensajes o <see cref="Sent"/>. Una fila anterior a PGTI-177
    /// no guarda id de mensaje y se queda en <see cref="Sent"/>.
    /// </summary>
    /// <param name="sendStatus">El <see cref="RecipientSendStatus"/> de la fila.</param>
    /// <param name="notificationIds">Los ids de mensaje que devolvió el envío.</param>
    /// <param name="known">Lo que notification-service contestó, por id de mensaje.</param>
    public static string? Of(
        string sendStatus,
        IEnumerable<Guid> notificationIds,
        IReadOnlyDictionary<Guid, string> known)
    {
        if (sendStatus != RecipientSendStatus.HandedOff)
            return null;

        var statuses = notificationIds
            .Select(id => known.TryGetValue(id, out var status) ? status : Sent)
            .ToHashSet(StringComparer.Ordinal);

        return Precedence.FirstOrDefault(statuses.Contains) ?? Sent;
    }
}

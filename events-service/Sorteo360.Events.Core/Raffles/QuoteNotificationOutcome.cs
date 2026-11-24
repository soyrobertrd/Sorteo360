namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Cómo terminó el intento de avisar por correo un momento de la cotización: el estado y, cuando
/// hubo envío, el puntero al lote de notification-service.
///
/// <para>
/// <b>No hay canal de error, y esa ausencia es el diseño.</b> Un <c>BaseError</c> es lo que un
/// endpoint le entrega a una persona, y esta falla no puede llegar nunca a una: una cotización
/// sometida está sometida aunque el correo se haya perdido (ADR-0020). Un tipo sin forma de
/// devolver error hace que tragarse la falla sea estructural, y no una regla que alguien vuelva a
/// romper dentro de seis meses —que es exactamente lo que pasó con <c>IApprovalGate</c>, cuyo
/// <c>Result</c> terminó tumbando el sometimiento que sí había funcionado—.
/// </para>
/// </summary>
/// <param name="Status">Uno de <see cref="QuoteNotificationStatus"/>.</param>
/// <param name="NotificationId">
/// El id del lote que aceptó notification-service, o <c>null</c>. Es nulo en los dos desenlaces que
/// no dejaron envío: no había a quién escribirle, o el envío no se pudo hacer.
/// </param>
public sealed record QuoteNotificationOutcome(string Status, string? NotificationId)
{
    /// <summary>notification-service aceptó el lote.</summary>
    public static QuoteNotificationOutcome Sent(string notificationId) =>
        new(QuoteNotificationStatus.Sent, notificationId);

    /// <summary>
    /// No se resolvió ningún destinatario, así que no se llamó a notification-service y no existe
    /// fila de historial en ninguna parte. El historial contesta <i>qué correo mandamos</i>, y un
    /// correo que nunca se intentó no es asunto suyo; que nadie se enteró sí es asunto de la
    /// cotización, y por eso viaja hasta su fila de intento.
    /// </summary>
    public static QuoteNotificationOutcome NoRecipient() =>
        new(QuoteNotificationStatus.NoRecipient, null);

    /// <summary>Se intentó mandar y no se pudo.</summary>
    public static QuoteNotificationOutcome Failed() =>
        new(QuoteNotificationStatus.Failed, null);
}

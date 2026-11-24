namespace Sorteo360.Convocatorias.Core.Notifications;

/// <summary>
/// Un correo transaccional listo para el transporte: un destinatario, nada que rastrear por
/// persona (ADR-0017). Es lo que manda convocatorias cuando contesta a un acto de la propia
/// persona —hoy la confirmación de una inscripción (spec <c>06</c> §4.4)— en vez de correr una
/// campaña.
/// <para>
/// <b>Convocatorias arma, no sustituye.</b> El asunto y el cuerpo viajan con sus marcadores
/// intactos, los valores del evento en una bolsa compartida y lo de la persona en la suya:
/// sustituir es trabajo del lado que recibe (spec <c>04</c> §2), igual que en el envío de una
/// campaña. La diferencia con <c>SendBatchRequest</c> es que acá no hay campaña, ni cursor de
/// reanudación, ni un desenlace por persona: un fallo del transporte no se anota en ninguna fila,
/// solo deja a la persona sin su correo, que es lo que §4.4 acepta a cambio de no tumbar la
/// inscripción.
/// </para>
/// </summary>
/// <param name="Type">
/// La etiqueta del tipo, guardada tal cual por el historial. No valida, no enruta y no elige
/// plantilla (spec <c>04</c> §2).
/// </param>
/// <param name="Language">El idioma, que dicta el evento.</param>
/// <param name="EventId">Sobre qué evento fue este correo. Es lo único que va en el <c>about</c>.</param>
/// <param name="IdempotencyKey">
/// Qué envío es este, acuñado por quien lo arma porque es quien sabe qué cuenta como el mismo
/// envío. Ver <see cref="NotificationIdempotencyKey"/>.
/// </param>
/// <param name="Subject">El asunto, con sus marcadores sin sustituir.</param>
/// <param name="BodyHtml">El correo completo en HTML, con sus marcadores sin sustituir.</param>
/// <param name="SharedData">Lo del evento: <c>evento</c>, <c>lugar</c> y <c>fecha</c>.</param>
/// <param name="RecipientEmail">A dónde va.</param>
/// <param name="RecipientName">
/// Nombre y apellido, para la cabecera <c>To</c> y el historial del emisor. Ver
/// <see cref="RecipientDisplayName"/>.
/// </param>
/// <param name="RecipientData">Solo lo suyo. Nunca una clave que también esté en <see cref="SharedData"/>.</param>
public sealed record TransactionalNotification(
    string Type,
    string Language,
    Guid EventId,
    string IdempotencyKey,
    string Subject,
    string BodyHtml,
    IReadOnlyDictionary<string, string> SharedData,
    string RecipientEmail,
    string? RecipientName,
    IReadOnlyDictionary<string, string> RecipientData);

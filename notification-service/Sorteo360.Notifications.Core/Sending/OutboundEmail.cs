namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Un mensaje listo para el socket: una persona, las dos partes del cuerpo y las cabeceras que
/// dependen de quién lo recibe. Una transacción SMTP por fila, sin <c>Cc</c> y sin <c>Bcc</c>:
/// sobre SMTP eso no es un caso especial, es el comportamiento nativo.
/// </summary>
/// <param name="RecipientId">
/// El id opaco de la fila de historial. Es lo único de la fila que viaja al proveedor, y viaja
/// dentro de <c>unique_args</c>.
/// </param>
/// <param name="FromAddress">Quién firma.</param>
/// <param name="FromName">Nombre visible del remitente, si lo hay.</param>
/// <param name="ToAddress">La dirección de la persona.</param>
/// <param name="ToName">El nombre visible de la persona en <c>To</c>, si quien llama lo mandó.</param>
/// <param name="Subject">Asunto ya sustituido.</param>
/// <param name="HtmlBody">Cuerpo HTML ya sustituido.</param>
/// <param name="TextBody">Alternativa de texto derivada del HTML.</param>
/// <param name="UnsubscribeUrl">
/// El enlace de baja, cuando la persona lo trae. Es lo que decide si el mensaje lleva
/// <c>List-Unsubscribe</c>: quien llama pone el enlace para quien está fuera del MITUR y no lo
/// pone para quien está dentro, así que el servicio distingue los dos casos sin saber nunca cuál
/// es cuál.
/// </param>
public sealed record OutboundEmail(
    Guid RecipientId,
    string FromAddress,
    string? FromName,
    string ToAddress,
    string? ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    string? UnsubscribeUrl);

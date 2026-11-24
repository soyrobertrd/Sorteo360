using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Errors;

/// <summary>
/// Los fallos de aceptar un envío. Todos llevan un código de máquina junto al estado HTTP: quien
/// consume ramifica por el código y escribe su propio español, porque el mensaje es prosa para
/// una persona y no un contrato.
/// <para>
/// Todos se contestan <b>antes</b> del 202 y sin haber guardado nada. Una petición mal formada es
/// un error nuestro; un fallo del mundo —el SMTP caído, un rebote, una supresión— se contesta 202
/// y se guarda con su estado.
/// </para>
/// </summary>
public static class NotificationErrors
{
    /// <summary>Código de la petición que no cumple la forma del contrato.</summary>
    public const string PayloadInvalidCode = "NOTIFICATION_PAYLOAD_INVALID";

    /// <summary>Tope de personas por envío. Es nuestro, no del proveedor.</summary>
    public const int RecipientCap = 1000;

    /// <summary>
    /// Más personas que el tope en una sola petición. Dividir en lotes es trabajo de quien llama:
    /// el tope existe porque un envío con muchas sustituciones conviene mandarlo en trozos, no
    /// porque el proveedor no admita más.
    /// </summary>
    public static BaseError RecipientCapExceeded(int count) => new(
        $"El envío trae {count} destinatarios y el máximo es {RecipientCap}; divida la petición en lotes.",
        "NOTIFICATION_RECIPIENT_CAP_EXCEEDED",
        ErrorKind.Invalid);

    /// <summary>
    /// La misma clave de idempotencia con otro contenido. Es un error nuestro, y los errores
    /// nuestros se ven: sin esta comprobación la clase entera de fallo sería invisible.
    /// </summary>
    public static BaseError IdempotencyKeyReused(string idempotencyKey) => new(
        $"La clave de idempotencia '{idempotencyKey}' ya identifica un envío con otro contenido.",
        "NOTIFICATION_IDEMPOTENCY_KEY_REUSED",
        ErrorKind.Conflict);
}

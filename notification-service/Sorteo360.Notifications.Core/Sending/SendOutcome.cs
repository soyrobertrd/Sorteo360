using Sorteo360.Notifications.Core.Notifications;

namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Lo que hay que escribir en la fila después de intentarla. Es un valor y no una escritura: quien
/// decide (el worker) y quien escribe (la cola) están separados, así que la regla de reintento se
/// puede probar sin base de datos.
/// </summary>
/// <param name="DeliveryStatus">El estado en que queda la fila.</param>
/// <param name="SmtpQueuedAs">
/// La respuesta con que el servidor aceptó el mensaje, verbatim: el <c>Ok: queued as &lt;token&gt;</c>
/// con el prefijo de estado que le ponga cada servidor. Es lo primero que pide el soporte del
/// proveedor. No es un campo de contrato ni una clave de join probada.
/// </param>
/// <param name="LastError">Por qué falló, cuando falló.</param>
/// <param name="NextAttemptAt">Cuándo toca reintentar, o null si no hay reintento.</param>
/// <param name="CountsAsAttempt">Si este resultado gasta un intento. Una supresión no lo gasta.</param>
public sealed record SendOutcome(
    string DeliveryStatus,
    string? SmtpQueuedAs,
    string? LastError,
    DateTimeOffset? NextAttemptAt,
    bool CountsAsAttempt)
{
    /// <summary>Si la fila ya no se vuelve a tocar. Es lo que dispara el borrado de sus valores.</summary>
    public bool IsTerminal => NextAttemptAt is null;

    /// <summary>El proveedor lo aceptó.</summary>
    public static SendOutcome Sent(string queuedAs) =>
        new(Notifications.DeliveryStatus.Sent, queuedAs, null, null, CountsAsAttempt: true);

    /// <summary>
    /// No se intentó porque la dirección está suprimida. Queda la fila diciéndolo, que es lo
    /// contrario de saltársela en silencio: el historial tiene que poder contestar "¿y por qué
    /// Juan no lo recibió?".
    /// </summary>
    public static SendOutcome Dropped(string reason) =>
        new(Notifications.DeliveryStatus.Dropped, null, reason, null, CountsAsAttempt: false);

    /// <summary>Falló y no se vuelve a intentar.</summary>
    public static SendOutcome Failed(string error) =>
        new(Notifications.DeliveryStatus.Failed, null, error, null, CountsAsAttempt: true);

    /// <summary>Falló y toca otra vez a la hora indicada; la fila sigue en cola.</summary>
    public static SendOutcome Retry(DateTimeOffset nextAttemptAt, string error) =>
        new(Notifications.DeliveryStatus.Queued, null, error, nextAttemptAt, CountsAsAttempt: true);
}

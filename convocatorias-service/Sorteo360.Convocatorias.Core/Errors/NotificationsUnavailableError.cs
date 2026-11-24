using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// notification-service no contesta, o contesta algo que no es el <c>202</c> del contrato.
/// <para>
/// <b>El lote entero se da por no entregado.</b> Antes del <c>202</c> no existe historia del otro
/// lado (spec <c>04</c> §2.1), así que no se anota a nadie y las filas se quedan <c>pending</c>:
/// es exactamente la distinción de la que vive el cursor de reanudación, porque <c>failed</c>
/// significa que el emisor miró a esa persona y la rechazó, y cualquier otra cosa deja la fila
/// intacta para que alguien la vuelva a mandar (§5).
/// </para>
/// <para>
/// <see cref="ErrorKind.Unavailable"/> y no <see cref="ErrorKind.Internal"/>: este servicio está
/// sano, quien falla es aquello de lo que depende. <b>Nadie lo ve como respuesta HTTP</b>: ocurre
/// en el worker, después del <c>202</c> del lanzamiento, así que lo que hace es quedar en el log y
/// dejar la campaña en <c>sending</c> a la espera de una persona.
/// </para>
/// </summary>
/// <param name="Detail">Qué pasó, para el log.</param>
public sealed record NotificationsUnavailableError(string Detail)
    : BaseError(
        $"El servicio de notificaciones no entregó el lote: {Detail}",
        ErrorCode,
        ErrorKind.Unavailable)
{
    public const string ErrorCode = "NOTIFICATIONS_UNAVAILABLE";
}

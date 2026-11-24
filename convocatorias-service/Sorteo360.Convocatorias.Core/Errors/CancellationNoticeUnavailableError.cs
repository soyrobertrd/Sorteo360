using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// No hay con qué armar el aviso de cancelación: falta la plantilla <c>event_cancelled</c> en el
/// idioma del evento (spec <c>06</c> §4.7). Se niega antes de reclamar ninguna fila, así que nadie
/// queda sellado sin haber recibido el correo y el aviso se puede volver a pedir cuando exista la
/// copia.
/// <para>
/// <see cref="ErrorKind.Unavailable"/>: el servicio está sano, lo que falta es la copia que una
/// persona escribe. Que falte no se resuelve cayendo al español (spec <c>02</c> §3).
/// </para>
/// </summary>
/// <param name="Detail">Qué faltó.</param>
public sealed record CancellationNoticeUnavailableError(string Detail)
    : BaseError(
        $"No se pudo armar el aviso de cancelación: {Detail}",
        ErrorCode,
        ErrorKind.Unavailable)
{
    public const string ErrorCode = "CANCELLATION_NOTICE_UNAVAILABLE";
}

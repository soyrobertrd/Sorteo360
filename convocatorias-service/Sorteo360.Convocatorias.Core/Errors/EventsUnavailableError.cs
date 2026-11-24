using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// events-service no contesta. Las tres lecturas que lo necesitan fallan enteras: el lanzamiento,
/// porque no sale un correo con un hueco donde va el nombre del evento, y las dos del portal,
/// porque servir una copia vieja sería servir una decisión de visibilidad vieja (spec <c>01</c>
/// §6).
/// <para>
/// <see cref="ErrorKind.Unavailable"/> y no <see cref="ErrorKind.Internal"/>: este servicio está
/// sano, quien falla es aquello de lo que depende.
/// </para>
/// </summary>
public sealed record EventsUnavailableError()
    : BaseError(
        "El servicio de eventos no está disponible.",
        ErrorCode,
        ErrorKind.Unavailable)
{
    public const string ErrorCode = "EVENTS_UNAVAILABLE";
}

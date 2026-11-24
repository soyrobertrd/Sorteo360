using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Ninguna inscripción tiene ese código QR (spec <c>09</c> §3.4). Cubre también lo que no tiene
/// forma de código —una URL entera de la entrada, por ejemplo—: separar el código del enlace es
/// trabajo del escáner, y esta ruta no adivina por él.
/// </summary>
public sealed record CheckInCodeNotFoundError()
    : BaseError(
        "Este código no corresponde a ninguna inscripción.",
        ErrorCode,
        ErrorKind.NotFound)
{
    public const string ErrorCode = "CHECK_IN_CODE_NOT_FOUND";
}

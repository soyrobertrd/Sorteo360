using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// El código QR es de una inscripción de otro evento (spec <c>09</c> §3.4). <b>No nombra a nadie</b>:
/// ni a la persona ni al evento de la entrada. En la puerta basta con oír "esta entrada no es de
/// este evento", y cualquier dato más sería dárselo a quien sostiene el teléfono de otro.
/// </summary>
public sealed record CheckInWrongEventError()
    : BaseError(
        "Esta entrada no corresponde a este evento.",
        ErrorCode,
        ErrorKind.Conflict)
{
    public const string ErrorCode = "CHECK_IN_WRONG_EVENT";
}

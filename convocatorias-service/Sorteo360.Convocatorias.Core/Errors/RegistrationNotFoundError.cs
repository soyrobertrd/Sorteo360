using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La inscripción no existe, o no es de este evento (spec <c>09</c> §4). Las dos cosas responden
/// igual a propósito: el id sale de la lista de la puerta de este evento o de la respuesta del
/// propio escaneo, así que uno de otro evento es un error del cliente, y un <c>404</c> no nombra nada.
/// <para>
/// El check-in por inscripción (§5) lo usa solo para el id que no existe: uno de otro evento es ahí
/// <see cref="CheckInWrongEventError"/>, como un QR de otro evento.
/// </para>
/// <para>
/// No es <see cref="PublicRegistrationNotFoundError"/>, que es del portal y no tiene que ver.
/// </para>
/// </summary>
public sealed record RegistrationNotFoundError()
    : BaseError(
        "La inscripción no existe en este evento.",
        ErrorCode,
        ErrorKind.NotFound)
{
    public const string ErrorCode = "REGISTRATION_NOT_FOUND";
}

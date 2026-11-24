using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// No se pudo armar el correo de confirmación de una inscripción: falta la plantilla
/// <c>registration_confirmation</c> en el idioma del evento (spec <c>06</c> §4.4).
/// <para>
/// <b>Nadie lo ve como respuesta HTTP.</b> La inscripción se guarda pase lo que pase y contesta
/// <c>201</c>; este fallo ocurre después, al mandar el correo, así que lo que hace es quedar en el
/// log. La persona se queda sin su correo, pero el QR está en la pantalla de confirmación y la
/// referencia impresa junto a él (spec <c>06</c> §4.4).
/// </para>
/// <para>
/// <see cref="ErrorKind.Unavailable"/>: el servicio está sano, lo que falta es la copia que una
/// persona escribe. Que falte no se resuelve cayendo al español (spec <c>02</c> §3).
/// </para>
/// </summary>
/// <param name="Detail">Qué faltó, para el log.</param>
public sealed record RegistrationConfirmationUnavailableError(string Detail)
    : BaseError(
        $"No se pudo armar la confirmación de la inscripción: {Detail}",
        ErrorCode,
        ErrorKind.Unavailable)
{
    public const string ErrorCode = "REGISTRATION_CONFIRMATION_UNAVAILABLE";
}

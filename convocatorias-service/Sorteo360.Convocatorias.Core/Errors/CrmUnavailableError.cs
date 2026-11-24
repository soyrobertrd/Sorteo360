using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// El CRM no contesta. La selección de destinatarios falla entera y no deja lista a medias: una
/// lista no se puede construir de la nada (spec <c>03</c> §6.2).
/// <para>
/// <see cref="ErrorKind.Unavailable"/> y no <see cref="ErrorKind.Internal"/>: este servicio
/// está sano, quien falla es aquello de lo que depende, y un 500 se lo atribuiría a él.
/// </para>
/// </summary>
public sealed record CrmUnavailableError()
    : BaseError(
        "El CRM no está disponible: no se pudo construir la lista de destinatarios.",
        ErrorCode,
        ErrorKind.Unavailable)
{
    public const string ErrorCode = "CRM_UNAVAILABLE";
}

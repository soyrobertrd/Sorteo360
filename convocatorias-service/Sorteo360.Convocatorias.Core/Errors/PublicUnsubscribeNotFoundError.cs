using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Ese enlace de baja no lleva a ninguna fila. <b>Un solo motivo y un solo código</b>: el token
/// no está. No hace falta la mezcla de motivos de <see cref="PublicRegistrationNotFoundError"/>
/// —acá solo puede fallar por token desconocido—, pero sí un mensaje propio, porque dar de baja
/// no es abrir una inscripción y el cliente ramifica por el código (spec <c>06</c> §1 y §6).
/// </summary>
public sealed record PublicUnsubscribeNotFoundError()
    : BaseError(
        "No se pudo dar de baja con ese enlace.",
        ErrorCode,
        ErrorKind.NotFound)
{
    public const string ErrorCode = "PUBLIC_UNSUBSCRIBE_NOT_FOUND";
}

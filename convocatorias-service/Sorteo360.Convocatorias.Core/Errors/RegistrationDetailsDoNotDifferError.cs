using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se pidió dar por revisada una ficha que coincide con el CRM (spec <c>07</c> §6.1). No hay nada
/// que revisar, y sellarla igual escondería el error de una pantalla que ofrece el botón donde no va.
/// </summary>
public sealed record RegistrationDetailsDoNotDifferError()
    : BaseError(
        "Los datos de esta inscripción coinciden con el CRM: no hay nada que revisar.",
        ErrorCode,
        ErrorKind.Conflict)
{
    public const string ErrorCode = "REGISTRATION_DETAILS_DO_NOT_DIFFER";
}

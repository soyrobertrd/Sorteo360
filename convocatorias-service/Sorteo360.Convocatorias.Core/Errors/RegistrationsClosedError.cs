using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La ventana de inscripción del evento está cerrada (spec <c>06</c> §5.3). Lo contestan resolver
/// un token y mandar el formulario.
/// <para>
/// <b>Dice su motivo, y el <see cref="PublicRegistrationNotFoundError"/> no.</b> Un evento
/// cancelado se esconde tras un <c>404</c> que no dice por qué; acá el evento es público, está en
/// la cartelera y su detalle se está sirviendo, así que un <c>404</c> contradiría la página de dos
/// clics atrás. Lo único que revela es cerrado frente a inexistente, que no es nada en una ruta
/// que solo sirve eventos públicos.
/// </para>
/// </summary>
public sealed record RegistrationsClosedError()
    : BaseError(
        "Las inscripciones para este evento están cerradas.",
        ErrorCode,
        ErrorKind.Conflict)
{
    public const string ErrorCode = "REGISTRATIONS_CLOSED";
}

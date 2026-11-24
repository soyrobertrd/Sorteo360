using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <summary>
/// La puerta de un evento (spec <c>09</c> §3 y §4). No pregunta a events-service por el estado del
/// evento: una caída ahí no puede parar la puerta (§3.3).
/// </summary>
public interface ICheckInService
{
    /// <summary>
    /// Hace el check-in de la inscripción de <paramref name="qrCode"/>, o informa el que ya tenía.
    /// <c>CHECK_IN_CODE_NOT_FOUND</c> si el código no es de nadie, <c>CHECK_IN_WRONG_EVENT</c> si es
    /// de otro evento.
    /// </summary>
    Task<Result<CheckIn, BaseError>> CheckInAsync(
        Guid eventId,
        string qrCode,
        Guid checkedInById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lo mismo que <see cref="CheckInAsync"/> con la inscripción elegida en la lista de acceso
    /// (spec <c>09</c> §5): otra forma de identificarla, no otra clase de check-in.
    /// <c>REGISTRATION_NOT_FOUND</c> si no existe, <c>CHECK_IN_WRONG_EVENT</c> si es de otro evento.
    /// </summary>
    Task<Result<CheckIn, BaseError>> CheckInByRegistrationAsync(
        Guid eventId,
        Guid registrationId,
        Guid checkedInById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deshace el check-in. Sale bien haya habido uno o no, para que un doble toque no sea un error;
    /// <c>REGISTRATION_NOT_FOUND</c> si la inscripción no existe en este evento.
    /// </summary>
    Task<UnitResult<BaseError>> UndoAsync(
        Guid eventId,
        Guid registrationId,
        Guid undoneById,
        CancellationToken cancellationToken);
}

using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>
/// La escritura de la puerta (spec <c>09</c> §3 y §4): hacer un check-in y deshacerlo. Cada condición
/// viaja dentro de su <c>UPDATE</c>, así que dos terminales a la vez nunca hacen dos check-ins.
/// </summary>
public interface ICheckInRepository
{
    /// <summary>La inscripción de ese código QR, o <see cref="Maybe{T}.None"/> si no hay ninguna.</summary>
    Task<Result<Maybe<CheckInTarget>, BaseError>> FindByQrCodeAsync(
        string qrCode,
        CancellationToken cancellationToken);

    /// <summary>La inscripción de ese id, o <see cref="Maybe{T}.None"/> si no existe en ningún evento.</summary>
    Task<Result<Maybe<CheckInTarget>, BaseError>> FindByIdAsync(
        Guid registrationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sella el check-in con <paramref name="now"/> y <paramref name="checkedInById"/> solo si la
    /// inscripción no tiene uno, y la relee. Si ya lo tenía, se queda con el primero.
    /// </summary>
    Task<Result<CheckInStamp, BaseError>> StampAsync(
        Guid registrationId,
        DateTimeOffset now,
        Guid checkedInById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Borra el check-in de la inscripción si es de ese evento, y sella <c>updated_at</c> con
    /// <paramref name="now"/> cuando borró algo. <see cref="Maybe{T}.None"/> es una inscripción que
    /// no existe o es de otro evento.
    /// </summary>
    Task<Result<Maybe<CheckInClearance>, BaseError>> ClearAsync(
        Guid eventId,
        Guid registrationId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

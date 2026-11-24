using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Quién está registrado en la puerta de un evento. La autoridad sobre la asistencia es
/// convocatorias, pero nada en esta firma lo dice: ni una ruta, ni un DTO de red. Que la lista
/// llegue por la red es un hecho de Infrastructure, y por eso los tests del sorteo la sustituyen
/// sin levantar nada.
/// </summary>
public interface IAttendanceSource
{
    /// <summary>
    /// Todas las personas registradas en la puerta del evento, en vivo.
    /// <para>
    /// Sin filtrar: quien llegó sin invitación, las direcciones <c>.do</c> y quien el CRM tiene
    /// vetado vienen todos. Clasificarlos es trabajo del sorteo, y la cuenta de la puerta tiene que
    /// cuadrar con calificados más no calificados. Tampoco hay techo aquí: el límite de filas es del
    /// sorteo, que es quien lo rechaza.
    /// </para>
    /// <para>
    /// Falla con <see cref="ErrorKind.Unavailable"/> cuando no se puede averiguar, y nunca devuelve
    /// una lista vacía en ese caso. Una lista vacía es "nadie llegó"; confundirla con "no se pudo
    /// saber" congelaría un bombo vacío sin que nadie se entere.
    /// </para>
    /// </summary>
    Task<Result<IReadOnlyList<Attendee>, BaseError>> GetCheckedInAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene los asistentes elegibles según <paramref name="requiresCheckIn"/> (A35/R2):
    /// con <c>true</c> solo quienes hicieron check-in; con <c>false</c> todo el padrón activo.
    /// </summary>
    Task<Result<IReadOnlyList<Attendee>, BaseError>> GetAttendeesAsync(
        Guid eventId,
        bool requiresCheckIn,
        CancellationToken cancellationToken) =>
        GetCheckedInAsync(eventId, cancellationToken);
}

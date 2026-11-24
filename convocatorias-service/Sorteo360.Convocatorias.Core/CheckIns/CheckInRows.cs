namespace Sorteo360.Convocatorias.Core.CheckIns;

// Lo que ICheckInRepository le devuelve a CheckInService. Son hechos sobre una fila; qué responder
// con ellos lo decide el servicio.

/// <summary>La inscripción de un código QR o de un id: lo justo para saber si es de este evento.</summary>
public sealed record CheckInTarget(Guid RegistrationId, Guid EventId);

/// <summary>
/// Una inscripción después de intentar sellarla. <see cref="Changed"/> dice si este intento la
/// selló; si no, el sello es el que ya tenía.
/// </summary>
public sealed record CheckInStamp(
    bool Changed,
    Guid RegistrationId,
    string FirstName,
    string LastName,
    string? Company,
    string Reference,
    DateTimeOffset CheckedInAt,
    Guid CheckedInById);

/// <summary>
/// Lo que borró una deshecha. <see cref="Cleared"/> es nulo cuando no había check-in que borrar.
/// <para>
/// Es un envoltorio y no un <c>Maybe&lt;ClearedCheckIn?&gt;</c> a propósito: <c>Maybe.From(null)</c>
/// es <c>None</c>, así que "no había nada que borrar" se confundiría con "la inscripción no existe".
/// </para>
/// </summary>
public sealed record CheckInClearance(ClearedCheckIn? Cleared);

/// <summary>El check-in que se deshizo, tal como estaba.</summary>
public sealed record ClearedCheckIn(DateTimeOffset CheckedInAt, Guid CheckedInById);

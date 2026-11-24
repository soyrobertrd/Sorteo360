namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <summary>
/// La respuesta de un escaneo (spec <c>09</c> §3.1), la misma la primera vez y las siguientes.
/// <b>No lleva el correo</b>: en la puerta se compara un nombre con un documento, y una dirección no
/// suma nada a eso y pondría las de todo un evento en una terminal.
/// </summary>
/// <param name="CheckedInAt">
/// El del primer escaneo, también en una repetición: es lo que deja decir "entró a las 18:42".
/// </param>
/// <param name="CheckedInById">El <c>sub</c> de quien hizo el check-in.</param>
/// <param name="AlreadyCheckedIn">Verdadero cuando este escaneo no lo hizo, porque ya estaba hecho.</param>
public sealed record CheckIn(
    Guid RegistrationId,
    string FirstName,
    string LastName,
    string? Company,
    string Reference,
    DateTimeOffset CheckedInAt,
    Guid CheckedInById,
    bool AlreadyCheckedIn);

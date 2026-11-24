namespace Sorteo360.Convocatorias.Core.DTOs;

/// <summary>
/// Lo que manda el formulario del portal (spec <c>06</c> §1 y §4.1).
/// <para>
/// <b><see cref="Token"/> es opcional y por eso hay un endpoint y no dos.</b> Con token se
/// valida y se ata la referencia; sin token se acepta solo si el evento es público, y
/// <c>campaign_recipient_id</c> queda en nulo. Dos rutas duplicarían cada validación y cada
/// límite de peticiones a cambio de una columna anulable (§1.1).
/// </para>
/// <para>
/// El nombre son <b>dos campos y no uno partido al enviar</b>: "Lic. Juan Pérez de la Cruz" no
/// tiene punto de corte fiable, y el alta del CRM pide nombre y apellido por separado.
/// </para>
/// </summary>
/// <param name="EventId">A qué evento se inscribe.</param>
/// <param name="Token">El token del enlace, si llegó por uno.</param>
/// <param name="FirstName">Su nombre.</param>
/// <param name="LastName">Su apellido.</param>
/// <param name="Email">Su dirección de correo, tal como la escribió.</param>
/// <param name="Phone">Su teléfono.</param>
/// <param name="Company">Su empresa, texto libre.</param>
/// <param name="JobTitle">Su cargo.</param>
/// <param name="Country">Su país.</param>
public sealed record RegisterDto(
    Guid EventId,
    string? Token,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Company,
    string? JobTitle,
    string? Country);

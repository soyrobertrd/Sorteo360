namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// La respuesta de un escaneo (spec <c>09</c> §3.1), la misma con <c>201</c> que con <c>200</c>.
/// <b>Sin correo</b>: en la puerta se compara un nombre con un documento.
/// </summary>
public sealed record CheckInResponse
{
    public required Guid RegistrationId { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string? Company { get; init; }

    public required string Reference { get; init; }

    /// <summary>El del primer escaneo, también cuando este es una repetición.</summary>
    public required DateTimeOffset CheckedInAt { get; init; }

    /// <summary>El <c>sub</c> de quien hizo el check-in.</summary>
    public required Guid CheckedInById { get; init; }

    /// <summary>Verdadero cuando este escaneo no lo hizo, porque ya estaba hecho.</summary>
    public required bool AlreadyCheckedIn { get; init; }
}

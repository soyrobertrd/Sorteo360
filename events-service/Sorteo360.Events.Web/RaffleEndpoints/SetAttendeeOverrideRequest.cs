namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Una decisión manual sobre un asistente. El correo va en el cuerpo y nunca en la ruta: en un
/// segmento de URL necesita codificarse y termina en los registros de acceso (spec 01 §9).
/// </summary>
public record SetAttendeeOverrideRequest
{
    public string? Email { get; init; }

    /// <summary><c>included</c> o <c>excluded</c>.</summary>
    public string? Decision { get; init; }

    /// <summary>Opcional, para el próximo operador. En blanco se guarda como nulo.</summary>
    public string? Note { get; init; }
}

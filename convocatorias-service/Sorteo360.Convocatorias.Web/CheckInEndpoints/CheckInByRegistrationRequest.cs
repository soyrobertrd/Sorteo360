namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>El check-in elegido en la lista de acceso (spec <c>09</c> §5): el id de la inscripción.</summary>
public sealed record CheckInByRegistrationRequest
{
    /// <summary>El <c>registrationId</c> de una fila de la lista de acceso.</summary>
    public Guid? RegistrationId { get; init; }
}

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>A quién se le retira la decisión manual. El correo va en el cuerpo y no en la ruta, igual que al escribirla.</summary>
public record RemoveAttendeeOverrideRequest
{
    public string? Email { get; init; }
}

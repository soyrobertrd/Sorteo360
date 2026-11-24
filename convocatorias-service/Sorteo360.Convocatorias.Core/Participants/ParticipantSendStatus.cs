namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>
/// El eje de entrega de una persona: los cuatro de <see cref="Campaigns.RecipientSendStatus"/>
/// más <see cref="None"/>.
/// </summary>
public static class ParticipantSendStatus
{
    /// <summary>
    /// Nadie le escribió a esta dirección. Es un hecho y no una ausencia (§3.2): lo lleva quien
    /// llegó sin invitación.
    /// </summary>
    public const string None = "none";
}

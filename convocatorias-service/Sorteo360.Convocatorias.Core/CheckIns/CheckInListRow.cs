using Sorteo360.Convocatorias.Core.Participants;

namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <summary>
/// Una inscripción con check-in, como la lee la rifa (spec <c>09</c> §7). Trae los dos hechos de los
/// que sale la procedencia y no la procedencia ya decidida: la regla es
/// <see cref="ParticipantOrigin.Of"/>, la misma de la lista de participantes, y no se copia (§7.1).
/// </summary>
/// <param name="EmailNormalized">La dirección recortada y en minúsculas, como la guarda la columna.</param>
/// <param name="ContactId">
/// El <c>crm_contact_id</c> de esta inscripción, nunca el del destinatario cuyo enlace usó. Nulo
/// mientras su escritura al CRM está pendiente (spec <c>07</c> §6.1).
/// </param>
/// <param name="IsRecipient">Su dirección es la de un destinatario de una campaña lanzada del evento.</param>
/// <param name="CameThroughLink">Se inscribió con el enlace de un destinatario.</param>
/// <param name="CheckedInAt">Cuándo entró.</param>
public sealed record CheckInListRow(
    Guid RegistrationId,
    string EmailNormalized,
    string FirstName,
    string LastName,
    string? Company,
    Guid? ContactId,
    bool IsRecipient,
    bool CameThroughLink,
    DateTimeOffset CheckedInAt)
{
    /// <summary>Uno de <see cref="ParticipantOrigin"/>.</summary>
    public string Origin => ParticipantOrigin.Of(IsRecipient, CameThroughLink);
}

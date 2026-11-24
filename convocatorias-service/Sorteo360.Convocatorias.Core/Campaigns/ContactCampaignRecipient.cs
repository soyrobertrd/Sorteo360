namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Una campaña lanzada que tuvo a un contacto del CRM en su lista (PGTI-176): el destinatario visto
/// desde el contacto y no desde la campaña. Es lo que la ficha de una empresa junta de sus contactos.
/// </summary>
/// <param name="ContactId">El <c>contact_id</c> del destinatario.</param>
/// <param name="CampaignId">La campaña.</param>
/// <param name="EventId">El evento que convoca; nulo en un mensaje directo (PGTI-174).</param>
/// <param name="Subject">El asunto de un mensaje directo; nulo en una campaña de evento.</param>
/// <param name="LaunchedAt">
/// Cuándo se lanzó la campaña. Es la fecha del envío para quien la lee, y con ella la empresa en la
/// que trabajaba el contacto ese día.
/// </param>
/// <param name="SendStatus">El estado de envío del destinatario, <see cref="RecipientSendStatus"/>.</param>
/// <param name="Registered">
/// Si hay una inscripción en el evento con la dirección a la que se le escribió, la misma unión de la
/// lista de participantes (spec <c>07</c> §3.1). Nulo sin evento: no había nada en qué inscribirse.
/// </param>
/// <param name="NotificationId">El id del mensaje en notification-service, si lo hay (PGTI-177).</param>
public sealed record ContactCampaignRecipient(
    Guid ContactId,
    Guid CampaignId,
    Guid? EventId,
    string? Subject,
    DateTimeOffset LaunchedAt,
    string SendStatus,
    bool? Registered,
    Guid? NotificationId = null);

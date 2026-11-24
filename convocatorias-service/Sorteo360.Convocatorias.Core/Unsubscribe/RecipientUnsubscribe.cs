namespace Sorteo360.Convocatorias.Core.Unsubscribe;

/// <summary>
/// La baja de una fila de destinatario vista desde la reconciliación (spec <c>07</c> §6.2): las
/// dos columnas anulables y a quién se le empuja. Pendiente es clic sin sello.
/// </summary>
/// <param name="RecipientId">La fila de <c>campaign_recipient</c>.</param>
/// <param name="CampaignId">Su campaña.</param>
/// <param name="ContactId">El contacto del CRM al que se le empuja la baja.</param>
/// <param name="UnsubscribedAt">Cuándo hizo clic, o nulo si nunca se dio de baja.</param>
/// <param name="UnsubscribeSyncedAt">Cuándo llegó la baja al CRM, o nulo si sigue pendiente.</param>
public sealed record RecipientUnsubscribe(
    Guid RecipientId,
    Guid CampaignId,
    Guid ContactId,
    DateTimeOffset? UnsubscribedAt,
    DateTimeOffset? UnsubscribeSyncedAt);

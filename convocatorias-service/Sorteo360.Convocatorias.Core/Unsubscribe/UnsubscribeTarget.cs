namespace Sorteo360.Convocatorias.Core.Unsubscribe;

/// <summary>
/// La fila de destinatario a la que apunta un token de baja: lo justo para empujar el opt-out al
/// CRM y decirle a la persona sobre qué dirección actuó, enmascarada.
/// </summary>
/// <param name="RecipientId">La fila de <c>campaign_recipient</c> que se sella.</param>
/// <param name="ContactId">La identidad en el CRM, que es a quien se le empuja la baja.</param>
/// <param name="Email">La dirección a la que se escribió, para enmascararla en la respuesta.</param>
/// <param name="TokenHash">El hash guardado, para la comparación en tiempo fijo.</param>
/// <param name="UnsubscribedAt">Cuándo se hizo el primer clic, o nulo si es el primero.</param>
/// <param name="UnsubscribeSyncedAt">Cuándo llegó la baja al CRM, o nulo si sigue pendiente.</param>
public sealed record UnsubscribeTarget(
    Guid RecipientId,
    Guid ContactId,
    string Email,
    byte[] TokenHash,
    DateTimeOffset? UnsubscribedAt,
    DateTimeOffset? UnsubscribeSyncedAt);

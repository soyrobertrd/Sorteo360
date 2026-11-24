namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Todo lo que un POST aceptado deja escrito, junto. Las cuatro partes se crean a la vez, se
/// guardan en una sola transacción y no significan nada por separado: una lista de personas sin su
/// envío no es nada, y un envío sin sus valores no se puede mandar.
/// </summary>
/// <param name="Batch">El correo tal como lo escribió quien llama.</param>
/// <param name="About">Las entidades de las que trata.</param>
/// <param name="Recipients">Las personas con dirección válida.</param>
/// <param name="Payloads">
/// Los valores de sustitución de cada persona, que el worker necesita después del 202 y que se
/// borran en cuanto su fila deja de ser reintentable.
/// </param>
public sealed record AcceptedSend(
    NotificationBatch Batch,
    IReadOnlyList<NotificationAbout> About,
    IReadOnlyList<NotificationRecipient> Recipients,
    IReadOnlyList<NotificationPayload> Payloads);

namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Una fila tomada de la cola, con todo lo que hace falta para armar su mensaje: la persona, el
/// correo tal como lo escribió quien llama —todavía con sus <c>{{placeholders}}</c>— y los valores
/// con los que se sustituye.
/// </summary>
/// <param name="RecipientId">Id opaco de la fila de historial.</param>
/// <param name="Email">La dirección.</param>
/// <param name="Name">El nombre al que va dirigido, si quien llama lo mandó.</param>
/// <param name="AttemptCount">Intentos que ya lleva, sin contar el que se va a hacer.</param>
/// <param name="QueuedAt">Cuándo entró en cola. Desde acá se cuenta la hora.</param>
/// <param name="BatchId">El envío del que forma parte.</param>
/// <param name="FromAddress">Quién firma.</param>
/// <param name="FromName">Nombre visible del remitente.</param>
/// <param name="Subject">Asunto sin sustituir.</param>
/// <param name="Body">Cuerpo HTML sin sustituir.</param>
/// <param name="Values">
/// Los valores de esta persona, ya mezclados con los comunes del envío. Viven en una fila aparte
/// que se borra en cuanto la fila deja de ser reintentable, así que el historial permanente nunca
/// los contiene.
/// </param>
public sealed record PendingSend(
    Guid RecipientId,
    string Email,
    string? Name,
    int AttemptCount,
    DateTimeOffset QueuedAt,
    Guid BatchId,
    string FromAddress,
    string? FromName,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string> Values);

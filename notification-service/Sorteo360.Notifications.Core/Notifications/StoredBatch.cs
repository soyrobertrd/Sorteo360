namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// El envío que quedó guardado bajo una clave de idempotencia: el recién creado, o el que ya
/// estaba cuando la clave se repite. Lleva su huella para que el servicio pueda distinguir una
/// repetición legítima de la misma clave usada con otro contenido.
/// </summary>
/// <param name="BatchId">Id del envío guardado.</param>
/// <param name="BodySha256">Huella de la petición con que se creó.</param>
/// <param name="Recipients">Las filas de persona que tiene, con sus ids.</param>
public sealed record StoredBatch(Guid BatchId, string BodySha256, IReadOnlyList<StoredRecipient> Recipients);

/// <summary>Una fila de persona ya guardada.</summary>
/// <param name="Id">Id opaco de la fila.</param>
/// <param name="Email">Dirección de la persona.</param>
public sealed record StoredRecipient(Guid Id, string Email);

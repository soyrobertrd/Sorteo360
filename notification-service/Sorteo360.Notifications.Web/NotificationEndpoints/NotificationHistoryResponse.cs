using System.Text.Json;

namespace Sorteo360.Notifications.Web.NotificationEndpoints;

/// <summary>Una entidad de la plataforma de la que trata el envío. Ids, nunca nombres.</summary>
public sealed record NotificationAboutResponse
{
    public required string SourceService { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
}

/// <summary>
/// Una fila del historial. Los campos del envío que la enmarcan van repetidos en cada fila a
/// propósito: una lista de correos en la que el asunto vive en otra respuesta no se puede leer.
/// <para>
/// <c>DeliveryStatus</c> es el estado reportado y <c>DeliveryTrackingAvailable</c> dice si puede
/// moverse: cuando es falso la pantalla muestra "seguimiento de entrega no disponible en este
/// entorno" en vez de leer <c>sent</c> como un final.
/// </para>
/// </summary>
public sealed record NotificationHistoryItemResponse
{
    public required Guid Id { get; init; }
    public required Guid BatchId { get; init; }
    public required string Type { get; init; }
    public required string Subject { get; init; }
    public required string Language { get; init; }
    public required string FromAddress { get; init; }
    public string? FromName { get; init; }
    public required string Email { get; init; }

    /// <summary>
    /// El nombre al que se dirigió el correo, tal como lo mandó quien llamó; null si no mandó
    /// ninguno. La pantalla muestra entonces la dirección, nunca un guion.
    /// </summary>
    public string? Name { get; init; }

    public required string DeliveryStatus { get; init; }
    public required string RecipientAction { get; init; }
    public required bool DeliveryTrackingAvailable { get; init; }
    public required DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }

    /// <summary>Cuántas veces se intentó. Es nuestro, para depurar, no para una pantalla.</summary>
    public required int AttemptCount { get; init; }

    /// <summary>El último fallo de SMTP. Nuestro.</summary>
    public string? LastError { get; init; }

    /// <summary>El id del mensaje en el proveedor. Nuestro.</summary>
    public string? SgMessageId { get; init; }

    /// <summary>
    /// El <c>250 Ok: queued as</c> que contestó el servidor. Nuestro, y <b>no</b> es una clave de
    /// join probada: es lo primero que pide el soporte del proveedor, y con eso basta.
    /// </summary>
    public string? SmtpQueuedAs { get; init; }

    public required IReadOnlyList<NotificationAboutResponse> About { get; init; }
}

/// <summary>
/// Un envío entero. Es lo que resuelve la cabecera <c>Location</c> de un 202, así que existe
/// independientemente de esta pantalla.
/// </summary>
public sealed record NotificationBatchResponse
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Subject { get; init; }

    /// <summary>
    /// El cuerpo <b>sin sustituir</b>, con sus <c>{{placeholders}}</c> intactos. Es lo único que
    /// existe: los valores no se guardan, y por eso tampoco hay reenvío.
    /// </summary>
    public required string Body { get; init; }

    public required string Language { get; init; }
    public required string FromAddress { get; init; }
    public string? FromName { get; init; }
    public required bool DeliveryTrackingAvailable { get; init; }

    /// <summary>Huella del cuerpo. Nuestra, para depurar.</summary>
    public required string BodySha256 { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<NotificationAboutResponse> About { get; init; }
    public required IReadOnlyList<NotificationHistoryItemResponse> Recipients { get; init; }
}

/// <summary>
/// Un evento del proveedor que no se pudo correlacionar con ninguna fila. Lleva el JSON crudo tal
/// como llegó, que es lo único con lo que se puede averiguar de quién era.
/// </summary>
public sealed record OrphanEventResponse
{
    public required Guid Id { get; init; }
    public string? SgEventId { get; init; }
    public required string EventType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>El JSON del proveedor, como JSON y no como texto con comillas escapadas.</summary>
    public required JsonElement Payload { get; init; }
}

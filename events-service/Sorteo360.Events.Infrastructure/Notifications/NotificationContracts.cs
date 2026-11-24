namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// El cuerpo que espera <c>POST /v1/notifications</c>.
/// <para>
/// Los valores son siempre cadenas. Un número en el cable le entregaría el símbolo de moneda y los
/// separadores a una sustitución que no los puede probar, y el formato pertenece al C# de quien
/// llama.
/// </para>
/// </summary>
public sealed record SendNotificationRequest
{
    /// <summary>Etiqueta de historial: <c>servicio.cosa.que-pasó</c>. Acá siempre la misma cadena que la clave de plantilla.</summary>
    public required string Type { get; init; }

    /// <summary>
    /// Idioma de la copia que se usó. <c>es</c> en los avisos de una cotización; el del evento, o
    /// <c>es</c> si no hay copia en ese idioma, en el correo al ganador.
    /// </summary>
    public required string Language { get; init; }

    public required string FromAddress { get; init; }

    public string? FromName { get; init; }

    /// <summary>Asunto de la plantilla, con sus marcadores <b>sin</b> sustituir.</summary>
    public required string Subject { get; init; }

    /// <summary>Cuerpo de la plantilla, con sus marcadores <b>sin</b> sustituir.</summary>
    public required string Body { get; init; }

    /// <summary>Valores que aplican a todas las personas del lote.</summary>
    public Dictionary<string, string> SharedData { get; init; } = [];

    public required List<NotificationRecipientRequest> Recipients { get; init; }

    public required List<NotificationAboutRequest> About { get; init; }

    /// <summary>
    /// Nombra este envío. Derivada de una clave natural y nunca aleatoria: una clave que se
    /// regenera en el reintento no es una clave.
    /// </summary>
    public required string IdempotencyKey { get; init; }
}

/// <summary>Una persona del envío, con los valores que solo le tocan a ella.</summary>
public sealed record NotificationRecipientRequest
{
    public required string Email { get; init; }

    /// <summary>
    /// Nombre al que va dirigido el correo. notification-service lo pone en la cabecera To y lo
    /// guarda en el historial; es independiente del marcador <c>{{nombre}}</c> del cuerpo.
    /// </summary>
    public string? Name { get; init; }

    public Dictionary<string, string> Data { get; init; } = [];
}

/// <summary>Una entidad de la plataforma de la que trata el envío. Nunca sale de la plataforma.</summary>
public sealed record NotificationAboutRequest
{
    public required string SourceService { get; init; }

    public required string EntityType { get; init; }

    public required string EntityId { get; init; }
}

/// <summary>Los ids del envío aceptado. Se lee el del lote y se descartan los de cada persona.</summary>
public sealed record SendNotificationResponse(Guid BatchId);

namespace Sorteo360.Notifications.Web.NotificationEndpoints;

/// <summary>
/// Un correo listo para mandar: quién lo firma, qué dice, con qué valores se completa, a quién va y
/// de qué trata.
/// <para>
/// Los valores son siempre cadenas: <c>"candidatos": "3"</c>, <c>"monto": "USD 45,000.00"</c>. Un
/// número en el cable le entregaría el símbolo de moneda y los separadores a una sustitución que no
/// los puede probar, y el formato pertenece al C# de quien llama.
/// </para>
/// <para>
/// Los campos que este servicio no conoce se ignoran, no se rechazan, para que un cliente más nuevo
/// pueda hablarle a un servicio más viejo.
/// </para>
/// </summary>
public record SendNotificationRequest
{
    /// <summary>Etiqueta de historial, en minúsculas y con puntos: <c>servicio.cosa.que-pasó</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Idioma de la copia. Obligatorio y sin valor por omisión.</summary>
    public string? Language { get; init; }

    /// <summary>Dirección que firma el correo.</summary>
    public string? FromAddress { get; init; }

    /// <summary>Nombre visible del remitente.</summary>
    public string? FromName { get; init; }

    /// <summary>Asunto, con sus marcadores.</summary>
    public string? Subject { get; init; }

    /// <summary>Cuerpo HTML completo, con sus marcadores. Sale tal cual después de sustituir.</summary>
    public string? Body { get; init; }

    /// <summary>Valores que aplican a todas las personas.</summary>
    public Dictionary<string, string>? SharedData { get; init; }

    /// <summary>Las personas del envío.</summary>
    public List<NotificationRecipientRequest>? Recipients { get; init; }

    /// <summary>Las entidades de la plataforma de las que trata el envío.</summary>
    public List<NotificationAboutRequest>? About { get; init; }

    /// <summary>
    /// Clave que nombra este envío, acuñada por quien llama y derivada de una clave natural: nunca
    /// aleatoria, porque una clave que se regenera en el reintento no es una clave.
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Una persona del envío. Los datos personales y cualquier credencial van acá, nunca dentro del
/// cuerpo: el cuerpo se guarda y los datos no.
/// </summary>
public record NotificationRecipientRequest
{
    /// <summary>Dirección de la persona.</summary>
    public string? Email { get; init; }

    /// <summary>
    /// Nombre al que va dirigido el correo, opcional. Es un dato del envío, como el asunto: va en
    /// la cabecera <c>To</c> y queda en el historial tal como llegó ese día.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>Valores propios de esta persona.</summary>
    public Dictionary<string, string>? Data { get; init; }
}

/// <summary>Una entidad de la plataforma de la que trata el envío. Nunca sale de la plataforma.</summary>
public record NotificationAboutRequest
{
    /// <summary>Servicio dueño de la entidad.</summary>
    public string? SourceService { get; init; }

    /// <summary>Tipo de entidad.</summary>
    public string? EntityType { get; init; }

    /// <summary>Id de la entidad en su servicio.</summary>
    public string? EntityId { get; init; }
}
